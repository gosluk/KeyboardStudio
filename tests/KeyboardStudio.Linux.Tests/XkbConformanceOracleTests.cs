using System.Text.RegularExpressions;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;
using Xunit.Abstractions;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Checks the importer's reading of a layout against libxkbcommon's.
///
/// Every other test in this suite grades the importer against something the project wrote: a fixture,
/// a golden, an expectation in an assertion. None of them can say whether the composition rules are
/// right — whether including <c>latin</c> and then overriding four keys leaves the same layout the
/// system would produce. Only the implementation everyone else's keyboard actually goes through can
/// answer that, so <c>xkbcli compile-keymap</c> is compiled against as an oracle: it flattens the
/// same layout out of the same files, and the two flattenings have to agree.
///
/// It reads the host's database rather than the vendored fixtures, so both sides are looking at
/// exactly the same bytes and a version difference cannot be mistaken for a defect. Skipped where
/// xkbcli is absent, and enforced in Linux CI.
/// </summary>
public sealed class XkbConformanceOracleTests(ITestOutputHelper output)
{
    private const string RootDirectory = "/usr/share/X11/xkb";

    /// <summary>
    /// Keys are compared to the depth the model holds. Levels beyond the fourth are dropped on
    /// import with a diagnostic, so a disagreement there is already reported rather than silent.
    /// </summary>
    private const int ComparedLevels = 4;

    private static readonly Regex KeyBlock = new(
        @"key\s+<(?<name>[^>]+)>\s*\{(?<body>[^{}]*)\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// The long form, which libxkbcommon prints for a key that also needed a type or actions.
    /// </summary>
    private static readonly Regex NamedSymbols = new(
        @"symbols\[(?:Group)?1\]\s*=\s*\[(?<symbols>[^\]]*)\]",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// The short form — <c>key &lt;AD01&gt; { [ q, Q ] };</c> — which is most of a keymap. The
    /// first list is the first group either way.
    /// </summary>
    private static readonly Regex BareSymbols = new(
        @"^[^=\[\]]*\[(?<symbols>[^\]]*)\]",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static TheoryData<string, string?> Layouts => new()
    {
        { "us", null },
        { "us", "intl" },
        { "pl", null },
        { "de", null },
        { "fr", "oss" },
        // A phonetic layout, which writes <LatQ> as well as <AD01> — two names for one key that
        // only the keycodes file relates. Getting this one wrong puts different letters on five
        // keys than the host does, and nothing in the layout says so.
        { "am", "phonetic" }
    };

    [Theory]
    [Trait("Category", "XkbIntegration")]
    [MemberData(nameof(Layouts))]
    public async Task Resolve_ForALayoutTheHostShips_AgreesWithXkbcliAboutEveryKeyItReads(
        string layoutId,
        string? variantId)
    {
        if (!TryLocate(out var executable))
        {
            return;
        }

        var theirs = Keysyms(ParseGroupOne(await CompileAsync(executable, layoutId, variantId)), layoutId);

        // A parse that found nothing would make every assertion below vacuous, and the output
        // format is libxkbcommon's to change.
        Assert.True(
            theirs.Count > 40,
            $"Read {theirs.Count} keys out of xkbcli's keymap for '{Describe(layoutId, variantId)}'; the output format has probably changed.");

        var resolved = Resolve(layoutId, variantId);
        Assert.NotNull(resolved);

        var ours = Keysyms(Statements(resolved!), layoutId);
        Assert.NotEmpty(ours);

        var decoder = new XkbKeysymDecoder();
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var (keyId, actual) in ours)
        {
            // Keys xkbcli has and the layout does not are the ones the rules add around it —
            // `pc` supplies the modifiers and the function row. They are not this layout's
            // statements, so they are not this layout's to be graded on.
            if (!theirs.TryGetValue(keyId, out var expected))
            {
                continue;
            }

            var depth = Math.Min(ComparedLevels, Math.Min(Trimmed(expected), Trimmed(actual)));

            for (var level = 0; level < depth; level++)
            {
                compared++;

                // Compared as decoded outputs rather than as names: xkbcli prints the canonical
                // name of a keysym and a symbols file may write any of its spellings, so
                // `U0105` and `aogonek` are the same answer written two ways.
                var left = decoder.Decode(expected[level]).Output;
                var right = decoder.Decode(actual[level]).Output;

                if (left != right)
                {
                    mismatches.Add(
                        $"{keyId} level {level + 1}: xkbcli says '{expected[level]}', we say '{actual[level]}'.");
                }
            }
        }

        output.WriteLine($"{Describe(layoutId, variantId)}: {compared} levels compared against xkbcli.");
        foreach (var mismatch in mismatches)
        {
            output.WriteLine(mismatch);
        }

        // A plain two-level layout compares around ninety levels, so anything much below that
        // means the sides stopped meeting rather than that they agreed.
        Assert.True(compared > 80, $"Only {compared} levels were compared; the two key sets barely overlap.");
        Assert.Empty(mismatches);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task Import_ThenGenerate_ProducesSymbolsXkbcliCompiles()
    {
        // The oracle in the other direction: whatever the importer takes out of the database has to
        // go back in as something the system's own compiler accepts. A layout that imports cleanly
        // and then cannot be built is no more use than one that fails to import.
        if (!TryLocate(out _) || !TryCreateSource(out var source))
        {
            return;
        }

        var result = await source.ImportAsync(
            new ImportableLayoutReference(XkbLayoutImportSource.SourceId, "pl", null, $"{RootDirectory}/symbols/pl"),
            LayoutImportOptions.Default);
        Assert.True(result.Success);

        var directory = Path.Combine(
            Directory.GetCurrentDirectory(), "TestResults", "xkb-integration", "imported-pl");
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        var build = await new BuildOrchestrator(
                new KeyboardProjectValidator(),
                new BuildBackendResolver([
                    new LinuxXkbBuildBackend(
                        new XkbLayoutMetadata("pl-imported", "basic", "Polish (imported)"),
                        requireExternalVerification: true)
                ]))
            .BuildAsync(result.Project!, new BuildOptions(BuildTarget.LinuxXkb, directory));

        var details = Assert.IsType<XkbBuildDetails>(build.Artifact?.BackendDetails);
        Assert.True(
            build.Success,
            $"{string.Join(Environment.NewLine, build.Artifact!.Diagnostics.Select(item => item.Message))}{Environment.NewLine}{details.Verification.StandardError}");
        Assert.Equal(XkbVerificationStatus.Verified, details.Verification.Status);

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>
    /// What libxkbcommon makes of the layout, as the text of a compiled keymap.
    /// </summary>
    private static async Task<string> CompileAsync(string executable, string layoutId, string? variantId)
    {
        List<string> arguments = ["compile-keymap", "--layout", layoutId];
        if (variantId is not null)
        {
            arguments.AddRange(["--variant", variantId]);
        }

        var result = await new ProcessRunner().RunAsync(
            new ProcessRequest(executable, arguments, RootDirectory, new Dictionary<string, string?>()));

        Assert.True(
            result.ExitCode == 0,
            $"xkbcli could not compile '{layoutId}': {result.StandardError}");

        return result.StandardOutput;
    }

    /// <summary>
    /// The first group's symbols for each key of a compiled keymap, in the order they are written.
    /// </summary>
    private static List<(string KeyName, string[] Keysyms)> ParseGroupOne(string keymap)
    {
        var keys = new List<(string, string[])>();

        foreach (Match block in KeyBlock.Matches(keymap))
        {
            var body = block.Groups["body"].Value;
            var symbols = NamedSymbols.Match(body);
            if (!symbols.Success)
            {
                // A key with actions and no symbols — a modifier — lists nothing to compare, and
                // its action list must not be read as one.
                symbols = body.Contains('=', StringComparison.Ordinal)
                    ? Match.Empty
                    : BareSymbols.Match(body);
            }

            if (!symbols.Success)
            {
                continue;
            }

            keys.Add((
                $"<{block.Groups["name"].Value}>",
                symbols.Groups["symbols"].Value
                    .Split(',')
                    .Select(symbol => symbol.Trim())
                    .Where(symbol => symbol.Length > 0)
                    .ToArray()));
        }

        return keys;
    }

    /// <summary>
    /// Both sides' keys addressed the way the host addresses them: by the physical key, not by the
    /// name a file happened to write.
    ///
    /// <c>keycodes/evdev</c> gives most keys more than one name, and a phonetic layout writes both
    /// — <c>&lt;LatQ&gt;</c> and <c>&lt;AD01&gt;</c> are one key, and the later statement is the one
    /// that takes effect. Comparing by name would credit us with a key xkbcli never mentions and
    /// grade the other one against a statement the host discarded.
    /// </summary>
    private static Dictionary<string, string[]> Keysyms(
        IEnumerable<(string KeyName, string[] Keysyms)> keys,
        string layoutId)
    {
        // The larger template, so that a key only ISO has is compared rather than dropped.
        const string TemplateId = "iso-105";

        var resolver = new XkbKeyNameResolver(
            new XkbKeyNameMapper(),
            XkbKeyNameResolver.AliasSetForLayout(layoutId));

        var byKeyId = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (keyName, keysyms) in keys)
        {
            if (resolver.Resolve(TemplateId, keyName).KeyId is { } keyId)
            {
                byKeyId[keyId] = keysyms;
            }
        }

        return byKeyId;
    }

    /// <summary>
    /// The resolved section's keys, in the order the resolver returns them.
    /// </summary>
    private static IEnumerable<(string KeyName, string[] Keysyms)> Statements(ResolvedXkbSymbols symbols) =>
        symbols.Keys.Select(key => (key.KeyName, Keysyms: key.Keysyms.ToArray()));

    /// <summary>
    /// How many levels of a list carry a symbol. Both sides pad to the width of the key's type, and
    /// trailing padding is not a disagreement about anything.
    /// </summary>
    private static int Trimmed(string[] keysyms)
    {
        var length = keysyms.Length;
        while (length > 0 && string.Equals(keysyms[length - 1], "NoSymbol", StringComparison.Ordinal))
        {
            length--;
        }

        return length;
    }

    private static ResolvedXkbSymbols? Resolve(string layoutId, string? variantId)
    {
        var fileSystem = new HostXkbFileSystem();
        XkbDataRoot[] roots = [new(RootDirectory, LayoutSourceOrigin.System)];

        return new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots))
            .Resolve(layoutId, variantId);
    }

    private static bool TryCreateSource(out XkbLayoutImportSource source)
    {
        var fileSystem = new HostXkbFileSystem();
        XkbDataRoot[] roots = [new(RootDirectory, LayoutSourceOrigin.System)];

        if (!OperatingSystem.IsLinux() || !fileSystem.DirectoryExists($"{RootDirectory}/symbols"))
        {
            source = null!;
            return false;
        }

        source = new XkbLayoutImportSource(
            fileSystem,
            new StaticDataRootLocator(roots),
            new XkbRulesRegistryReader(fileSystem),
            new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots)),
            new XkbKeyNameMapper(),
            new XkbKeysymDecoder(),
            new KeyboardTemplateProvider());

        return true;
    }

    /// <summary>
    /// Finds xkbcli, or decides whether its absence is a skip or a broken CI image.
    /// </summary>
    private static bool TryLocate(out string executable)
    {
        if (OperatingSystem.IsLinux() && new PathXkbCliLocator().Find() is { } found &&
            new HostXkbFileSystem().DirectoryExists($"{RootDirectory}/symbols"))
        {
            executable = found;
            return true;
        }

        if (OperatingSystem.IsLinux() &&
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail("xkbcli and an installed XKB database are required for XkbIntegration tests in Linux CI.");
        }

        executable = null!;
        return false;
    }

    private static string Describe(string layoutId, string? variantId) =>
        variantId is null ? layoutId : $"{layoutId}({variantId})";

    private sealed class StaticDataRootLocator(IReadOnlyList<XkbDataRoot> roots) : IXkbDataRootLocator
    {
        public IReadOnlyList<XkbDataRoot> Locate() => roots;
    }
}
