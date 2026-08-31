using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Imports layouts from the XKB database the host has installed.
///
/// The catalog is the union of two listings. <c>rules/evdev.xml</c> supplies everything a list of
/// several hundred entries needs to be searched and grouped, and <c>symbols/</c> supplies the
/// layouts the registry omits — those are importable too, just nameless, and are reported under
/// <see cref="LayoutImportDiagnosticCodes.LayoutMetadataUnavailable"/> when one is imported.
/// </summary>
public sealed class XkbLayoutImportSource : ILayoutImportSource
{
    private readonly IXkbFileSystem _fileSystem;
    private readonly IXkbDataRootLocator _dataRootLocator;
    private readonly IXkbLayoutRegistryReader _registryReader;
    private readonly IXkbSymbolsResolver _symbolsResolver;
    private readonly IXkbKeyNameMapper _keyNameMapper;
    private readonly IXkbKeysymDecoder _keysymDecoder;
    private readonly IKeyboardTemplateProvider _templateProvider;

    public XkbLayoutImportSource(
        IXkbFileSystem fileSystem,
        IXkbDataRootLocator dataRootLocator,
        IXkbLayoutRegistryReader registryReader,
        IXkbSymbolsResolver symbolsResolver,
        IXkbKeyNameMapper keyNameMapper,
        IXkbKeysymDecoder keysymDecoder,
        IKeyboardTemplateProvider templateProvider)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _dataRootLocator = dataRootLocator ?? throw new ArgumentNullException(nameof(dataRootLocator));
        _registryReader = registryReader ?? throw new ArgumentNullException(nameof(registryReader));
        _symbolsResolver = symbolsResolver ?? throw new ArgumentNullException(nameof(symbolsResolver));
        _keyNameMapper = keyNameMapper ?? throw new ArgumentNullException(nameof(keyNameMapper));
        _keysymDecoder = keysymDecoder ?? throw new ArgumentNullException(nameof(keysymDecoder));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
    }

    /// <summary>
    /// The source's identifier, written into saved documents as provenance. Changing it orphans
    /// the provenance of every document already imported through it.
    /// </summary>
    public const string SourceId = "linux-xkb";

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public string DisplayName => "Installed XKB layouts";

    /// <inheritdoc />
    /// <remarks>
    /// A root with no <c>symbols/</c> directory holds nothing importable, so the source reports
    /// itself unavailable rather than offering an empty catalog.
    /// </remarks>
    public bool IsAvailable =>
        _dataRootLocator.Locate().Any(root => _fileSystem.DirectoryExists(root.SymbolsDirectory));

    /// <inheritdoc />
    public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var roots = _dataRootLocator.Locate();

        // Where each layout actually lives. The roots are already in libxkbcommon's precedence
        // order, so the first root holding a name is the one that wins and later copies of it are
        // shadowed rather than listed twice.
        var symbolsByLayout = new Dictionary<string, (string Path, LayoutSourceOrigin Origin)>(StringComparer.Ordinal);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var path in _fileSystem.EnumerateFiles(root.SymbolsDirectory))
            {
                symbolsByLayout.TryAdd(Path.GetFileName(path), (path, root.Origin));
            }
        }

        var descriptors = new List<ImportableLayoutDescriptor>(symbolsByLayout.Count);
        var described = new HashSet<string>(StringComparer.Ordinal);
        var listed = new HashSet<(string LayoutId, string? VariantId)>();

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var entry in ReadRegistry(root))
            {
                // The registry describes layouts no root implements — `custom` is one the
                // distribution ships for the user to write themselves. Listing an entry that
                // cannot be imported only offers the user a dead end.
                if (!symbolsByLayout.TryGetValue(entry.LayoutId, out var symbols) ||
                    !listed.Add((entry.LayoutId, entry.VariantId)))
                {
                    continue;
                }

                described.Add(entry.LayoutId);

                descriptors.Add(new ImportableLayoutDescriptor(
                    Id,
                    entry.LayoutId,
                    entry.VariantId,
                    entry.DisplayName,
                    entry.ShortDescription,
                    entry.Languages,
                    entry.Countries,
                    symbols.Origin,
                    symbols.Path));
            }
        }

        foreach (var (layoutId, symbols) in symbolsByLayout)
        {
            if (described.Contains(layoutId) || !listed.Add((layoutId, null)))
            {
                continue;
            }

            // Nothing describes this layout, so it is listed under its own file name. That is the
            // whole of what is known about it until someone imports it.
            descriptors.Add(new ImportableLayoutDescriptor(
                Id,
                layoutId,
                VariantId: null,
                layoutId,
                ShortDescription: null,
                Languages: [],
                Countries: [],
                symbols.Origin,
                symbols.Path));
        }

        descriptors.Sort(static (left, right) =>
        {
            var byLayout = string.CompareOrdinal(left.LayoutId, right.LayoutId);
            return byLayout != 0
                ? byLayout
                : string.CompareOrdinal(left.VariantId, right.VariantId);
        });

        return Task.FromResult<IReadOnlyList<ImportableLayoutDescriptor>>(descriptors);
    }

    /// <inheritdoc />
    public Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        // A null variant means the file's own default section, which is a section flagged `default`
        // and almost never one named it. Passing the word through as a section name would look for
        // a section no symbols file has.
        var symbols = _symbolsResolver.ResolveLayout(reference.LayoutId, reference.VariantId);
        if (symbols is null)
        {
            return Task.FromResult(LayoutImportResult.Failed(new LayoutImportReport(
                LayoutImportFidelity.Partial,
                KeysImported: 0,
                KeysSkipped: 0,
                ResolvedIncludeChain: [],
                [
                    new LayoutImportDiagnostic(
                        ValidationSeverity.Error,
                        LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                        $"No installed XKB root defines '{Describe(reference)}'.")
                ])));
        }

        var registryEntry = FindRegistryEntry(reference, cancellationToken);

        // The alias set is chosen from the layout name the way rules/evdev chooses it, so a
        // phonetic layout written for a German keyboard does not come back with Y and Z swapped.
        var keyNameResolver = new XkbKeyNameResolver(
            _keyNameMapper,
            XkbKeyNameResolver.AliasSetForLayout(reference.LayoutId));

        var importer = new XkbLayoutImporter(keyNameResolver, _keysymDecoder, _templateProvider);
        var result = importer.Import(symbols, options, registryEntry);

        if (registryEntry is null)
        {
            result = result with
            {
                Report = result.Report with
                {
                    Diagnostics =
                    [
                        new LayoutImportDiagnostic(
                            ValidationSeverity.Info,
                            LayoutImportDiagnosticCodes.LayoutMetadataUnavailable,
                            $"The registry does not describe '{Describe(reference)}', so it was imported under its own name."),
                        .. result.Report.Diagnostics
                    ]
                }
            };
        }

        return Task.FromResult(result);
    }

    private XkbRegistryEntry? FindRegistryEntry(
        ImportableLayoutReference reference,
        CancellationToken cancellationToken)
    {
        foreach (var root in _dataRootLocator.Locate())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var entry in ReadRegistry(root))
            {
                if (string.Equals(entry.LayoutId, reference.LayoutId, StringComparison.Ordinal) &&
                    string.Equals(entry.VariantId, reference.VariantId, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Reads one root's registry, treating a malformed one as absent. A distribution shipping a
    /// broken <c>evdev.xml</c> costs the user that root's names, not the whole catalog.
    /// </summary>
    private IReadOnlyList<XkbRegistryEntry> ReadRegistry(XkbDataRoot root)
    {
        try
        {
            return _registryReader.Read(root);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return [];
        }
    }

    private static string Describe(ImportableLayoutReference reference) =>
        reference.VariantId is null
            ? reference.LayoutId
            : $"{reference.LayoutId}({reference.VariantId})";
}
