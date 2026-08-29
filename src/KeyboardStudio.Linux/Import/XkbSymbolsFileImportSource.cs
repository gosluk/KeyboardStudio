using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Imports one symbols file the user pointed at, wherever it happens to be.
///
/// It lists nothing: a file outside the XKB roots is not something to browse for, it is something
/// the user names. <see cref="ListAsync"/> therefore returns an empty catalog and the whole of the
/// source's work happens in <see cref="ImportAsync"/>, from the path on the reference.
///
/// It is a separate source rather than a mode of <see cref="XkbLayoutImportSource"/> because the
/// two answer different questions — "what can I import?" against "import this" — and because
/// provenance should record which of the two a document came from. A layout that came out of the
/// installed database can be found again by name; a loose file can only be found again by path.
/// </summary>
public sealed class XkbSymbolsFileImportSource : ILayoutImportSource
{
    private readonly IXkbFileSystem _fileSystem;
    private readonly IXkbDataRootLocator _dataRootLocator;
    private readonly IXkbKeyNameMapper _keyNameMapper;
    private readonly IXkbKeysymDecoder _keysymDecoder;
    private readonly IKeyboardTemplateProvider _templateProvider;

    public XkbSymbolsFileImportSource(
        IXkbFileSystem fileSystem,
        IXkbDataRootLocator dataRootLocator,
        IXkbKeyNameMapper keyNameMapper,
        IXkbKeysymDecoder keysymDecoder,
        IKeyboardTemplateProvider templateProvider)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _dataRootLocator = dataRootLocator ?? throw new ArgumentNullException(nameof(dataRootLocator));
        _keyNameMapper = keyNameMapper ?? throw new ArgumentNullException(nameof(keyNameMapper));
        _keysymDecoder = keysymDecoder ?? throw new ArgumentNullException(nameof(keysymDecoder));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
    }

    /// <summary>
    /// The source's identifier. It is written into saved documents as provenance, so it is a
    /// constant rather than a literal: a caller building a reference by hand has to name the same
    /// string the source answers to.
    /// </summary>
    public const string SourceId = "linux-xkb-file";

    /// <inheritdoc />
    public string Id => SourceId;

    /// <inheritdoc />
    public string DisplayName => "Symbols file";

    /// <inheritdoc />
    /// <remarks>
    /// Available where the host has an XKB database, even though the file itself lies outside it.
    /// Symbols files are written as differences — a national layout is <c>latin</c> plus a dozen
    /// keys — so without the database to complete them, importing one yields those dozen keys and
    /// a report full of missing includes.
    /// </remarks>
    public bool IsAvailable => _dataRootLocator.Locate().Count > 0;

    /// <inheritdoc />
    /// <remarks>Nothing to list: this source imports what it is given, not what it can find.</remarks>
    public Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ImportableLayoutDescriptor>>([]);
    }

    /// <inheritdoc />
    /// <param name="reference">
    /// <see cref="ImportableLayoutReference.SourceLocation"/> is the path of the file and is
    /// required; <see cref="ImportableLayoutReference.VariantId"/> names a section, or is null for
    /// the file's default one. <see cref="ImportableLayoutReference.LayoutId"/> is used only to
    /// choose the phonetic key-alias set, the way <c>rules/evdev</c> chooses it from a layout name.
    /// </param>
    /// <param name="options">The caller's choices, or <see cref="LayoutImportOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels a long-running import.</param>
    public Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        var path = reference.SourceLocation;
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.FileExists(path))
        {
            return Task.FromResult(Failed(
                path is null
                    ? "No file was given to import."
                    : $"There is no file at '{path}'."));
        }

        // The file answers to its own name so that a section it includes from itself — pl(lefty)
        // including pl(basic) — resolves back to the file the user picked rather than to the
        // installed layout that happens to share its name.
        var fileName = Path.GetFileName(path);
        var resolver = new XkbSymbolsResolver(
            _fileSystem,
            new XkbPinnedFileIncludeResolver(
                new XkbIncludeResolver(_fileSystem, _dataRootLocator.Locate()),
                fileName,
                path));

        var symbols = resolver.Resolve(fileName, reference.VariantId);
        if (symbols is null)
        {
            return Task.FromResult(Failed(reference.VariantId is null
                ? $"'{path}' has no default symbols section."
                : $"'{path}' has no section named '{reference.VariantId}'."));
        }

        var keyNameResolver = new XkbKeyNameResolver(
            _keyNameMapper,
            XkbKeyNameResolver.AliasSetForLayout(reference.LayoutId));

        var importer = new XkbLayoutImporter(keyNameResolver, _keysymDecoder, _templateProvider);

        // A loose file is never in the registry, so there is never a description to import it
        // under and never a country hint to choose its geometry from. Both are said once, here,
        // rather than inferred from the file's contents.
        var result = importer.Import(symbols, options, registryEntry: null);

        return Task.FromResult(result with
        {
            Report = result.Report with
            {
                Diagnostics =
                [
                    new LayoutImportDiagnostic(
                        ValidationSeverity.Info,
                        LayoutImportDiagnosticCodes.LayoutMetadataUnavailable,
                        $"'{path}' is not in the registry, so it was imported under its own name."),
                    .. result.Report.Diagnostics
                ]
            }
        });
    }

    private static LayoutImportResult Failed(string message) =>
        LayoutImportResult.Failed(new LayoutImportReport(
            LayoutImportFidelity.Partial,
            KeysImported: 0,
            KeysSkipped: 0,
            ResolvedIncludeChain: [],
            [
                new LayoutImportDiagnostic(
                    ValidationSeverity.Error,
                    LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                    message)
            ]));
}
