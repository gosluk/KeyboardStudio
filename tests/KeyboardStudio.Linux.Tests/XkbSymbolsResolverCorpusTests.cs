using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Resolves every section of the host's <c>symbols/</c> corpus. Composition is the part of XKB that
/// hand-written cases model worst: the real chains reach four files deep and cross subdirectories,
/// and only the corpus shows whether the resolver follows them. Skipped where no XKB database is
/// installed, and enforced in Linux CI.
/// </summary>
public sealed class XkbSymbolsResolverCorpusTests
{
    private const string RootDirectory = "/usr/share/X11/xkb";
    private const string SymbolsDirectory = $"{RootDirectory}/symbols";

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Resolve_OverTheInstalledCorpus_ResolvesEverySectionAndEveryIncludeItNames()
    {
        if (!TryEnumerateCorpus(out var files))
        {
            return;
        }

        var resolver = CreateResolver();
        var parser = new XkbSymbolsParser();
        var unresolved = new List<string>();
        var unavailable = new List<string>();
        var keys = 0;

        foreach (var path in files)
        {
            var file = Path.GetRelativePath(SymbolsDirectory, path);

            foreach (var section in parser.Parse(path, File.ReadAllText(path)).Sections)
            {
                var resolved = resolver.Resolve(file, section.Name);
                if (resolved is null)
                {
                    unresolved.Add($"{file}({section.Name})");
                    continue;
                }

                keys += resolved.Keys.Count;
                unavailable.AddRange(resolved.Diagnostics
                    .Where(diagnostic => diagnostic.Code is LayoutImportDiagnosticCodes.CompositionTargetUnavailable
                        or LayoutImportDiagnosticCodes.CompositionDepthExceeded)
                    .Select(diagnostic => $"{file}({section.Name}): {diagnostic.Message}"));
            }
        }

        // A stock xkeyboard-config is internally consistent: every include it writes resolves, and
        // nothing in it is circular. Either list filling up means the resolver lost its way, not
        // that the distribution shipped a broken layout.
        Assert.Empty(unresolved);
        Assert.Empty(unavailable);
        Assert.True(keys > 50_000, $"Expected a full corpus; resolved only {keys} keys.");
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Resolve_ForAComposedLayout_FlattensTheWholeChainIntoOneSetOfKeys()
    {
        if (!TryEnumerateCorpus(out _))
        {
            return;
        }

        // pl(basic) is a real four-deep chain: it composes latin, which composes kpdl and level3.
        // Its own file defines only the keys Polish changes, so a resolver that failed to follow
        // the chain would still return a plausible-looking result with most of the alphabet gone.
        var resolved = CreateResolver().Resolve("pl", "basic");

        Assert.NotNull(resolved);
        Assert.Equal("Polish", resolved.DisplayName);
        Assert.Equal("pl(basic)", resolved.IncludeChain[0]);
        Assert.Contains("latin(basic)", resolved.IncludeChain);
        Assert.True(resolved.Keys.Count > 40, $"Expected a full alphabet; got {resolved.Keys.Count} keys.");

        // <AE01> is defined by latin and never by pl, so its presence proves the chain was followed.
        var digitOne = Assert.Single(resolved.Keys, key => key.KeyName == "<AE01>");
        Assert.Equal(["1", "exclam", "notequal", "exclamdown"], digitOne.Keysyms);

        // <AD01> is defined by both; Polish must win, and must say so.
        var q = Assert.Single(resolved.Keys, key => key.KeyName == "<AD01>");
        Assert.Equal("pl(basic)", q.Origin);
    }

    private static XkbSymbolsResolver CreateResolver()
    {
        var fileSystem = new HostXkbFileSystem();
        var roots = new[] { new XkbDataRoot(RootDirectory, LayoutSourceOrigin.System) };
        return new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots));
    }

    private static bool TryEnumerateCorpus(out IReadOnlyList<string> files)
    {
        if (OperatingSystem.IsLinux() && Directory.Exists(SymbolsDirectory))
        {
            files = Directory.GetFiles(SymbolsDirectory, "*", SearchOption.AllDirectories);
            return true;
        }

        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            && OperatingSystem.IsLinux())
        {
            Assert.Fail($"'{SymbolsDirectory}' is required for XkbIntegration tests in Linux CI.");
        }

        files = [];
        return false;
    }
}
