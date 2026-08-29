using KeyboardStudio.Core;
using KeyboardStudio.Linux;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// The pinned XKB database the golden, round-trip, and oracle tests import from.
///
/// The host's own database is the only input that proves the importer copes with real data, and it
/// is useless for asserting exact output: it changes under the tests every time the distribution
/// updates xkeyboard-config. So the layouts whose imports are pinned come from a copy vendored into
/// <c>Fixtures/Xkb</c> instead, and the corpus tests keep answering the other question against
/// whatever the host happens to have.
/// </summary>
internal static class VendoredXkbFixture
{
    /// <summary>Where the vendored database sits once the test project has been built.</summary>
    public static string Root { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Xkb");

    /// <summary>
    /// The vendored database read as the application reads the host's: registry, symbols, includes
    /// and all. Nothing here is a stub — only the root differs.
    /// </summary>
    public static XkbLayoutImportSource CreateSource()
    {
        var fileSystem = new HostXkbFileSystem();
        XkbDataRoot[] roots = [new(Root, LayoutSourceOrigin.System)];

        return new XkbLayoutImportSource(
            fileSystem,
            new StaticDataRootLocator(roots),
            new XkbRulesRegistryReader(fileSystem),
            new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots)),
            new XkbKeyNameMapper(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());
    }

    /// <summary>
    /// The loose-file source, pointed at the same database so that a file being re-imported can
    /// still complete itself from <c>latin</c> and <c>level3</c> the way any symbols file does.
    /// </summary>
    public static XkbSymbolsFileImportSource CreateFileSource()
    {
        var fileSystem = new HostXkbFileSystem();
        XkbDataRoot[] roots = [new(Root, LayoutSourceOrigin.System)];

        return new XkbSymbolsFileImportSource(
            fileSystem,
            new StaticDataRootLocator(roots),
            new XkbKeyNameMapper(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());
    }

    /// <summary>
    /// Replaces the absolute path of the vendored root with a placeholder.
    ///
    /// Include chains and several diagnostics quote the path a definition was read from, and that
    /// path is different on every machine that runs the suite. Without this, a golden would record
    /// the developer's home directory and fail everywhere else.
    /// </summary>
    public static string Anonymize(string text) =>
        text.Replace(Root, "{xkb}", StringComparison.Ordinal)
            .Replace('\\', '/');

    private sealed class StaticDataRootLocator(IReadOnlyList<XkbDataRoot> roots) : IXkbDataRootLocator
    {
        public IReadOnlyList<XkbDataRoot> Locate() => roots;
    }
}
