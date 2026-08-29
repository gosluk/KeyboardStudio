using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Runs the parser over the host's whole <c>symbols/</c> corpus. Hand-written cases cover the
/// grammar the parser was designed against; only the real corpus covers the grammar it will
/// actually meet. Skipped where no XKB database is installed, and enforced in Linux CI.
/// </summary>
public sealed class XkbSymbolsCorpusTests
{
    private const string SymbolsDirectory = "/usr/share/X11/xkb/symbols";

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Parse_OverTheInstalledCorpus_ReadsEveryFileWithoutFailingOrLosingASection()
    {
        if (!TryEnumerateCorpus(out var files))
        {
            return;
        }

        var parser = new XkbSymbolsParser();
        var empty = new List<string>();
        var keys = 0;

        foreach (var path in files)
        {
            var parsed = parser.Parse(path, File.ReadAllText(path));
            if (parsed.Sections.Count == 0)
            {
                empty.Add(path);
            }

            keys += parsed.Sections.Sum(section => section.Statements.OfType<XkbKeyStatement>().Count());
        }

        Assert.Empty(empty);
        Assert.True(keys > 10_000, $"Expected a full corpus; read only {keys} keys.");
    }

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public void Parse_OverTheInstalledCorpus_RecognizesEveryStatementItMeets()
    {
        // KSI022 means the parser met a construct nobody taught it. The real corpus raises none, so
        // any appearance here is a genuine gap in the grammar rather than acceptable noise.
        if (!TryEnumerateCorpus(out var files))
        {
            return;
        }

        var parser = new XkbSymbolsParser();
        var unrecognized = files
            .Select(path => parser.Parse(path, File.ReadAllText(path)))
            .SelectMany(parsed => parsed.Diagnostics)
            .Where(diagnostic => diagnostic.Code == LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped)
            .Select(diagnostic => diagnostic.Message)
            .ToList();

        Assert.Empty(unrecognized);
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
