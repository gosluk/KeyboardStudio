using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Imports keyboard layouts from the host's XKB database, discovering available layouts from
/// <c>rules/evdev.xml</c> and composing their symbols from <c>symbols/</c>.
/// </summary>
public sealed class XkbLayoutImportSource : ILayoutImportSource
{
    private readonly IXkbDataRootLocator _dataRootLocator;
    private readonly IXkbLayoutRegistryReader _registryReader;
    private readonly IXkbSymbolsResolver _symbolsResolver;
    private readonly XkbLayoutImporter _importer;

    public string Id => "linux-xkb";
    public string DisplayName => "XKB Database";

    public bool IsAvailable
    {
        get
        {
            var roots = _dataRootLocator.Locate();
            return roots.Count > 0;
        }
    }

    public XkbLayoutImportSource(
        IXkbDataRootLocator dataRootLocator,
        IXkbLayoutRegistryReader registryReader,
        IXkbSymbolsResolver symbolsResolver,
        IXkbKeyNameResolver keyNameResolver,
        IXkbKeysymDecoder keysymDecoderInstance,
        IKeyboardTemplateProvider templateProvider)
    {
        _dataRootLocator = dataRootLocator ?? throw new ArgumentNullException(nameof(dataRootLocator));
        _registryReader = registryReader ?? throw new ArgumentNullException(nameof(registryReader));
        _symbolsResolver = symbolsResolver ?? throw new ArgumentNullException(nameof(symbolsResolver));

        var nameResolver = keyNameResolver ?? throw new ArgumentNullException(nameof(keyNameResolver));
        var keysymDecoder = keysymDecoderInstance ?? throw new ArgumentNullException(nameof(keysymDecoderInstance));
        var templateProvider2 = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));

        _importer = new XkbLayoutImporter(nameResolver, keysymDecoder, templateProvider2);
    }

    public async Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        // Read the registry from all available roots.
        var roots = _dataRootLocator.Locate();
        var allEntries = new List<XkbRegistryEntry>();

        foreach (var root in roots)
        {
            try
            {
                var entries = _registryReader.Read(root);
                allEntries.AddRange(entries);
            }
            catch
            {
                // If one root fails, continue with others.
            }
        }

        var descriptors = new List<ImportableLayoutDescriptor>();

        // Deduplicate: if the same layout/variant appears in multiple roots, use the first.
        var seen = new HashSet<(string, string?)>();

        foreach (var entry in allEntries.OrderBy(e => e.LayoutId).ThenBy(e => e.VariantId ?? string.Empty))
        {
            var key = (entry.LayoutId, entry.VariantId);
            if (!seen.Add(key))
            {
                continue;
            }

            var location = ResolveSymbolsLocation(entry.LayoutId);

            var descriptor = new ImportableLayoutDescriptor(
                Id,
                entry.LayoutId,
                entry.VariantId,
                entry.DisplayName,
                entry.ShortDescription,
                entry.Languages,
                entry.Countries,
                DetermineOrigin(entry),
                location);

            descriptors.Add(descriptor);
        }

        return await Task.FromResult(descriptors);
    }

    public async Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = new List<LayoutImportDiagnostic>();

        try
        {
            // Determine which section to load: the variant name, or "default" if no variant.
            var section = reference.VariantId ?? "default";

            // Resolve the symbols, including all compositions.
            var resolved = _symbolsResolver.Resolve(reference.LayoutId, section);

            if (resolved == null)
            {
                diagnostics.Add(new LayoutImportDiagnostic(
                    ValidationSeverity.Error,
                    LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                    $"Could not resolve layout '{reference.LayoutId}{(reference.VariantId != null ? $"({reference.VariantId})" : "")}'"));
                return LayoutImportResult.Failed(
                    new LayoutImportReport(
                        LayoutImportFidelity.Partial,
                        KeysImported: 0,
                        KeysSkipped: 0,
                        [],
                        diagnostics));
            }

            // Find the registry entry for hints (from all roots).
            XkbRegistryEntry? registryEntry = null;
            var roots = _dataRootLocator.Locate();
            foreach (var root in roots)
            {
                try
                {
                    var entries = _registryReader.Read(root);
                    registryEntry = entries.FirstOrDefault(e =>
                        e.LayoutId == reference.LayoutId &&
                        e.VariantId == reference.VariantId);
                    if (registryEntry != null)
                    {
                        break;
                    }
                }
                catch
                {
                    // Continue searching other roots.
                }
            }

            // Import it.
            var result = _importer.Import(resolved, options, registryEntry);

            return await Task.FromResult(result);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new LayoutImportDiagnostic(
                ValidationSeverity.Error,
                LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
                $"Failed to import layout: {ex.Message}"));

            return LayoutImportResult.Failed(
                new LayoutImportReport(
                    LayoutImportFidelity.Partial,
                    KeysImported: 0,
                    KeysSkipped: 0,
                    [],
                    diagnostics));
        }
    }

    private string ResolveSymbolsLocation(string layoutId)
    {
        var roots = _dataRootLocator.Locate();
        foreach (var root in roots)
        {
            var path = Path.Combine(root.Path, "symbols", layoutId);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return $"symbols/{layoutId}";
    }

    private static LayoutSourceOrigin DetermineOrigin(XkbRegistryEntry entry)
    {
        // For now, all entries are system origin. In the future, this could be determined from
        // whether the file is in /usr/share vs ~/.local or similar.
        return LayoutSourceOrigin.System;
    }
}
