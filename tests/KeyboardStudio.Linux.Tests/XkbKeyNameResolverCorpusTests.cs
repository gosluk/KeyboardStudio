using System.Text.RegularExpressions;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Checks the resolver against the two things it is a transcription of: the key names the host's
/// <c>symbols/</c> corpus actually writes, and the aliases the host's own <c>keycodes/</c> and
/// <c>rules/</c> files declare. Hand-written cases can only confirm the aliases we already thought
/// of; these confirm the ones xkeyboard-config has. Skipped where no XKB database is installed, and
/// enforced in Linux CI.
/// </summary>
public sealed class XkbKeyNameResolverCorpusTests
{
    private const string RootDirectory = "/usr/share/X11/xkb";
    private const string SymbolsDirectory = $"{RootDirectory}/symbols";
    private const string TemplateId = "iso-105";

    /// <summary>
    /// Names on the four alphanumeric rows that a 105-key keyboard genuinely does not have.
    /// <c>&lt;AE13&gt;</c> is the yen key and <c>&lt;AB11&gt;</c> the underscore key of a Japanese
    /// keyboard, <c>&lt;AB00&gt;</c> the extra key of a Brazilian one, and the <c>&lt;AA*&gt;</c>
    /// row belongs to Sun terminals. Anything else appearing here would be a key this keyboard has
    /// under a name the resolver failed to recognise, which is exactly the failure this test is
    /// looking for.
    /// </summary>
    private static readonly HashSet<string> RowKeysNotOnAPc =
    [
        "<AA00>", "<AA02>", "<AA03>", "<AA06>", "<AA07>",
        "<AB00>", "<AB11>", "<AC00>", "<AE00>", "<AE13>"
    ];

    private static readonly Regex RowKeyName = new(@"^<A[ABCDE][0-9]{2}>$", RegexOptions.Compiled);

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Resolve_OverTheInstalledCorpus_LandsEveryKeyThisKeyboardHas()
    {
        if (!TryEnumerateCorpus(out var files))
        {
            return;
        }

        var resolver = new XkbKeyNameResolver();
        var symbols = CreateSymbolsResolver();
        var parser = new XkbSymbolsParser();
        var resolved = 0;
        var skipped = 0;
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var path in files)
        {
            var file = Path.GetRelativePath(SymbolsDirectory, path);

            foreach (var section in parser.Parse(path, File.ReadAllText(path)).Sections)
            {
                var layout = symbols.Resolve(file, section.Name);
                if (layout is null)
                {
                    continue;
                }

                foreach (var key in layout.Keys)
                {
                    if (resolver.Resolve(TemplateId, key.KeyName).Resolved)
                    {
                        resolved++;
                        continue;
                    }

                    skipped++;
                    unresolved.Add(key.KeyName);
                }
            }
        }

        // Every unresolved name must be a key that is genuinely absent from a 105-key keyboard:
        // media keys, vendor keys, F13 and up, and the extra keys of Japanese, Brazilian and Sun
        // keyboards. A name on one of the alphanumeric rows reaching this point would mean a key
        // the user does have was dropped.
        var missed = unresolved.Where(name => RowKeyName.IsMatch(name) && !RowKeysNotOnAPc.Contains(name)).ToArray();
        Assert.Empty(missed);

        // The host's corpus currently lands 66,151 keys and skips 7,360, all of the latter on keys
        // no PC keyboard has. The floor guards against a resolver that quietly stops recognising
        // names, the ceiling against one that starts finding reasons to drop them; both leave room
        // for a distribution's corpus to differ from this one.
        Assert.True(resolved > 60_000, $"Expected the full corpus to resolve; landed only {resolved} keys.");
        Assert.True(skipped < 10_000, $"Skipped {skipped} of {resolved + skipped} keys, which is more than expected.");
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Resolve_ForARealPhoneticLayout_PutsTheLettersWhereTheHostPutsThem()
    {
        if (!TryEnumerateCorpus(out _))
        {
            return;
        }

        // ru(phonetic) writes <LatZ>, and symbols/de writes it for the same sound on a keyboard
        // where that name means a different key. Reading both with one alias set would move a
        // letter on one of them.
        var qwerty = new XkbKeyNameResolver(new XkbKeyNameMapper(), XkbKeyNameResolver.AliasSetForLayout("ru"));
        var qwertz = new XkbKeyNameResolver(new XkbKeyNameMapper(), XkbKeyNameResolver.AliasSetForLayout("de"));
        var symbols = CreateSymbolsResolver();

        var russian = symbols.Resolve("ru", "phonetic");
        Assert.NotNull(russian);
        var latZ = Assert.Single(russian.Keys, key => key.KeyName == "<LatZ>");
        Assert.Equal("KeyZ", qwerty.Resolve(TemplateId, latZ.KeyName).KeyId);

        var german = symbols.Resolve("de", "ru");
        Assert.NotNull(german);
        var germanLatZ = Assert.Single(german.Keys, key => key.KeyName == "<LatZ>");
        Assert.Equal("KeyY", qwertz.Resolve(TemplateId, germanLatZ.KeyName).KeyId);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Resolve_ForEveryAliasTheHostDeclares_TreatsBothNamesAsOneKey()
    {
        if (!TryEnumerateCorpus(out _))
        {
            return;
        }

        var resolver = new XkbKeyNameResolver();
        var disagreements = new List<string>();

        foreach (var (first, second) in ReadAliases($"{RootDirectory}/keycodes/evdev", section: null))
        {
            var left = resolver.Resolve(TemplateId, first).KeyId;
            var right = resolver.Resolve(TemplateId, second).KeyId;

            // Either both names reach the same key of this keyboard or neither reaches one at all.
            // One resolving alone would mean a layout writing the other name loses the key; both
            // resolving to different keys would mean the table contradicts the host.
            if (left != right)
            {
                disagreements.Add($"{first} -> {left ?? "(skipped)"}, {second} -> {right ?? "(skipped)"}");
            }
        }

        Assert.Empty(disagreements);
    }

    [Theory]
    [Trait("Category", "XkbIntegration")]
    [InlineData(XkbKeyAliasSet.Qwerty, "qwerty")]
    [InlineData(XkbKeyAliasSet.Azerty, "azerty")]
    [InlineData(XkbKeyAliasSet.Qwertz, "qwertz")]
    public void Resolve_ForEveryPhoneticAliasTheHostDeclares_PlacesItOnTheSameKey(
        XkbKeyAliasSet aliasSet,
        string section)
    {
        if (!TryEnumerateCorpus(out _))
        {
            return;
        }

        var resolver = new XkbKeyNameResolver(new XkbKeyNameMapper(), aliasSet);
        var misplaced = new List<string>();

        foreach (var (first, second) in ReadAliases($"{RootDirectory}/keycodes/aliases", section))
        {
            var left = resolver.Resolve(TemplateId, first).KeyId;
            var right = resolver.Resolve(TemplateId, second).KeyId;
            if (left != right)
            {
                misplaced.Add($"{first} -> {left ?? "(skipped)"}, {second} -> {right ?? "(skipped)"}");
            }
        }

        Assert.Empty(misplaced);
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void AliasSetForLayout_AgreesWithTheHostRules()
    {
        if (!TryEnumerateCorpus(out _))
        {
            return;
        }

        // The lists live in rules/evdev, which the importer does not otherwise read. Diffing them
        // here is what catches a distribution moving a country between the sets, which would
        // otherwise show up only as two letters swapped on one layout.
        var rules = File.ReadAllLines($"{RootDirectory}/rules/evdev");
        var azerty = ReadRuleList(rules, "azerty");
        var qwertz = ReadRuleList(rules, "qwertz");

        Assert.Equal(azerty, SortedLayoutsFor(XkbKeyAliasSet.Azerty, azerty));
        Assert.Equal(qwertz, SortedLayoutsFor(XkbKeyAliasSet.Qwertz, qwertz));

        // And nothing else claims a set: every other layout the registry offers has to fall to
        // qwerty, or a layout would be read with letters in positions the host never puts them in.
        var named = azerty.Concat(qwertz).ToHashSet(StringComparer.Ordinal);
        var claimed = new XkbRulesRegistryReader(new HostXkbFileSystem())
            .Read(new XkbDataRoot(RootDirectory, LayoutSourceOrigin.System))
            .Select(entry => entry.LayoutId)
            .Distinct(StringComparer.Ordinal)
            .Where(layout => !named.Contains(layout))
            .Where(layout => XkbKeyNameResolver.AliasSetForLayout(layout) != XkbKeyAliasSet.Qwerty)
            .ToArray();

        Assert.Empty(claimed);
    }

    private static string[] SortedLayoutsFor(XkbKeyAliasSet aliasSet, IEnumerable<string> layouts) =>
        layouts
            .Where(layout => XkbKeyNameResolver.AliasSetForLayout(layout) == aliasSet)
            .OrderBy(layout => layout, StringComparer.Ordinal)
            .ToArray();

    private static string[] ReadRuleList(IReadOnlyList<string> rules, string name)
    {
        var line = rules.FirstOrDefault(text => text.TrimStart().StartsWith($"! ${name}", StringComparison.Ordinal));
        Assert.NotNull(line);

        return line[(line.IndexOf('=') + 1)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .OrderBy(layout => layout, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Reads the <c>alias &lt;A&gt; = &lt;B&gt;;</c> statements of a keycodes file, optionally from
    /// one named section of it. Deliberately a regular expression over the text rather than a
    /// parser: keycodes files are not a format the importer reads, and a test that shared a parser
    /// with the code under test would prove less than one that does not.
    /// </summary>
    private static IEnumerable<(string First, string Second)> ReadAliases(string path, string? section)
    {
        var inSection = section is null;

        foreach (var line in File.ReadLines(path))
        {
            if (section is not null && line.Contains("xkb_keycodes", StringComparison.Ordinal))
            {
                inSection = line.Contains($"\"{section}\"", StringComparison.Ordinal);
            }

            if (!inSection)
            {
                continue;
            }

            var match = Regex.Match(line.Trim(), @"^alias\s+(<[^>]+>)\s*=\s*(<[^>]+>)\s*;");
            if (match.Success)
            {
                yield return (match.Groups[1].Value, match.Groups[2].Value);
            }
        }
    }

    private static XkbSymbolsResolver CreateSymbolsResolver()
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
