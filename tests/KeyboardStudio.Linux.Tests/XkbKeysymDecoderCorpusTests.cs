using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Decodes every keysym the host's <c>symbols/</c> corpus actually writes. Hand-written cases pin
/// the forms the decoder was designed around; only the corpus says whether the generated table
/// covers what layouts really use, which is the question that decides whether an import comes out
/// whole or full of blanks. Skipped where no XKB database is installed, and enforced in Linux CI.
/// </summary>
public sealed class XkbKeysymDecoderCorpusTests
{
    private const string RootDirectory = "/usr/share/X11/xkb";
    private const string SymbolsDirectory = $"{RootDirectory}/symbols";

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Decode_OverTheInstalledCorpus_UnderstandsEveryKeysymItWrites()
    {
        if (!TryEnumerateCorpus(out var files))
        {
            return;
        }

        var decoder = new XkbKeysymDecoder();
        var notAKeysym = new SortedSet<string>(StringComparer.Ordinal);
        var unrepresentable = new SortedSet<string>(StringComparer.Ordinal);
        var characters = 0;
        var specialKeys = 0;
        var deadKeys = 0;

        foreach (var keysym in EnumerateKeysyms(files))
        {
            switch (decoder.Decode(keysym).Outcome)
            {
                case XkbKeysymDecodeOutcome.Character:
                    characters++;
                    break;
                case XkbKeysymDecodeOutcome.Key:
                    specialKeys++;
                    break;
                case XkbKeysymDecodeOutcome.DeadKey:
                    deadKeys++;
                    break;
                case XkbKeysymDecodeOutcome.NotRepresentable:
                    unrepresentable.Add(keysym);
                    break;
                case XkbKeysymDecodeOutcome.NotAKeysym:
                    notAKeysym.Add(keysym);
                    break;
            }
        }

        // The whole point of a generated table: every keysym xkeyboard-config names, the decoder
        // recognises. The exceptions are listed rather than tolerated as a count, because each one
        // is a specific upstream mistake and a new one arriving should be looked at.
        //
        //  - symbols/th writes Voidsymbol for VoidSymbol. libxkbcommon's keymap parser matches
        //    keysym names case-sensitively, so the user's own machine reads it as nothing too.
        //  - symbols/in writes keysyms in the 0x0100_0000 range whose characters land in the C1
        //    control block, 0x1000082 being U+0082, and comments them as Devanagari letters.
        Assert.All(
            notAKeysym,
            keysym => Assert.True(
                keysym == "Voidsymbol" || keysym.StartsWith("0x100", StringComparison.Ordinal),
                $"'{keysym}' is written by the corpus but the decoder does not recognise it."));

        Assert.True(characters > 150_000, $"Expected the full corpus; decoded only {characters} characters.");
        Assert.True(specialKeys > 12_000, $"Expected the full corpus; decoded only {specialKeys} keys.");
        Assert.True(deadKeys > 6_000, $"Expected the full corpus; found only {deadKeys} dead keys.");

        // Media keys, IME keys, F25 and above, and the vendor keysyms are what the corpus writes
        // that the model has no place for: 505 distinct names against 173,528 decoded characters.
        // Bounded rather than listed, because the exact set moves with the installed
        // xkeyboard-config; a jump means the table regressed rather than that a layout changed.
        Assert.True(
            unrepresentable.Count < 600,
            $"More keysyms are unrepresentable than expected: {string.Join(", ", unrepresentable.Take(40))}");
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Decode_ForTheUnderscoredMediaKeysyms_ReadsThemAsTheHostDoes()
    {
        if (!TryEnumerateCorpus(out _))
        {
            return;
        }

        // symbols/xfree86 writes XF86_Switch_VT_1, which no header defines: XKeysymDB spelt these
        // with a separating underscore and libxkbcommon still strips it. Pinned against the corpus
        // rather than as a unit case because it is the corpus that proves layouts still write them.
        var decoder = new XkbKeysymDecoder();

        Assert.Equal(XkbKeysymDecodeOutcome.NotRepresentable, decoder.Decode("XF86_Switch_VT_1").Outcome);
        Assert.Equal(XkbKeysymDecodeOutcome.NotRepresentable, decoder.Decode("XF86_ClearGrab").Outcome);
    }

    /// <summary>
    /// Every keysym of every key of every section, read through the resolver so that what is
    /// decoded is what an import would actually be handed.
    /// </summary>
    private static IEnumerable<string> EnumerateKeysyms(IReadOnlyList<string> files)
    {
        var fileSystem = new HostXkbFileSystem();
        var roots = new[] { new XkbDataRoot(RootDirectory, LayoutSourceOrigin.System) };
        var resolver = new XkbSymbolsResolver(fileSystem, new XkbIncludeResolver(fileSystem, roots));
        var parser = new XkbSymbolsParser();

        foreach (var path in files)
        {
            var file = Path.GetRelativePath(SymbolsDirectory, path);

            foreach (var section in parser.Parse(path, File.ReadAllText(path)).Sections)
            {
                var resolved = resolver.Resolve(file, section.Name);
                if (resolved is null)
                {
                    continue;
                }

                foreach (var key in resolved.Keys)
                {
                    foreach (var keysym in key.Keysyms)
                    {
                        yield return keysym;
                    }
                }
            }
        }
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
