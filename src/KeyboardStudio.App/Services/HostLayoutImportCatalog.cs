using KeyboardStudio.Core;
using KeyboardStudio.Linux;

namespace KeyboardStudio.App;

/// <summary>
/// Builds the import catalog for the host the application is running on.
///
/// This is the composition root for import, and the only place in the application that names a
/// concrete source. Everything above it — the dialog, its view model, the document service — works
/// in terms of <see cref="ILayoutImportCatalog"/> and descriptors, so a second platform's source
/// can be registered here without any of them changing.
/// </summary>
public static class HostLayoutImportCatalog
{
    public static ILayoutImportCatalog Create(IKeyboardTemplateProvider templateProvider)
    {
        ArgumentNullException.ThrowIfNull(templateProvider);

        var fileSystem = new HostXkbFileSystem();
        var dataRootLocator = new XkbDataRootLocator(new HostXkbEnvironment(), fileSystem);
        var keyNameMapper = new XkbKeyNameMapper();
        var keysymDecoder = new XkbKeysymDecoder();

        // The roots are read once, here, because the resolver's parse cache is only worth having
        // if it outlives a single import — browsing a catalog re-reads `latin` and `us` for
        // practically every entry. A database appearing mid-session is not a case worth paying for.
        var roots = dataRootLocator.Locate();

        return new LayoutImportCatalog(
        [
            new XkbLayoutImportSource(
                fileSystem,
                dataRootLocator,
                new XkbRulesRegistryReader(fileSystem),
                new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots)),
                keyNameMapper,
                keysymDecoder,
                templateProvider),
            new XkbSymbolsFileImportSource(
                fileSystem,
                dataRootLocator,
                keyNameMapper,
                keysymDecoder,
                templateProvider)
        ]);
    }

    /// <summary>
    /// Builds the probe that says which layout this host is configured to type with, so a freshly
    /// started editor can open onto it rather than onto something generic.
    /// </summary>
    public static IHostLayoutProbe CreateHostProbe() =>
        new XkbHostLayoutProbe(new XkbActiveLayoutProbe(new HostXkbEnvironment(), new HostXkbFileSystem()));

    /// <summary>
    /// Describes a file the user picked, so it can be imported through the same catalog, dialog and
    /// commit path as an entry the host advertises.
    /// </summary>
    public static ImportableLayoutDescriptor DescribeFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var name = Path.GetFileName(fullPath);

        return new ImportableLayoutDescriptor(
            XkbSymbolsFileImportSource.SourceId,
            name,
            VariantId: null,
            name,
            fullPath,
            Languages: [],
            Countries: [],
            LayoutSourceOrigin.File,
            fullPath);
    }
}
