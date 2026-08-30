using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserBundleGeneratorTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public async Task Generate_PolishVariant_MatchesTheGoldenBundleFiles()
    {
        var result = XkbUserBundleGenerator.Generate([Polish()]);

        Assert.True(result.Success);
        await AssertGoldenAsync(
            "symbols-keyboardstudio.xkb",
            result.Bundle!.Find("symbols/keyboardstudio")!.Content);
        await AssertGoldenAsync(
            "symbols-pl.xkb",
            result.Bundle.Find("symbols/pl")!.Content);
        await AssertGoldenAsync(
            "evdev.xml",
            result.Bundle.Find("rules/evdev.xml")!.Content);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_SeveralLayoutsAndProjects_GroupsDeterministicBridgesAndRegistryEntries()
    {
        var secondPolish = Variant(
            "8d42e6a3b20f41b5c1f075a12b306246",
            "pl",
            "dvorak",
            "dvorak",
            "keyboardstudio_dvorak",
            "Polish Dvorak - KeyboardStudio",
            []);
        var albanian = Variant(
            "9e53f7b4c31042c6d2a186b23c417357",
            "al",
            null,
            "basic",
            "keyboardstudio_basic",
            "Albanian - KeyboardStudio",
            []);

        var first = XkbUserBundleGenerator.Generate([secondPolish, albanian, Polish()]);
        var second = XkbUserBundleGenerator.Generate([Polish(), secondPolish, albanian]);

        Assert.True(first.Success);
        Assert.Equal(
            [
                "symbols/keyboardstudio",
                "symbols/al",
                "symbols/pl",
                "rules/evdev.xml",
                "keyboardstudio-bundle.json"
            ],
            first.Bundle!.Files.Select(file => file.RelativePath));
        Assert.Contains("xkb_symbols \"keyboardstudio_dvorak\"", first.Bundle.Find("symbols/pl")!.Content);
        Assert.Contains("xkb_symbols \"keyboardstudio_programmer\"", first.Bundle.Find("symbols/pl")!.Content);
        Assert.Contains("<name>al</name>", first.Bundle.Find("rules/evdev.xml")!.Content);
        Assert.Contains("<name>pl</name>", first.Bundle.Find("rules/evdev.xml")!.Content);
        Assert.Equal(
            first.Bundle.Files.Select(file => (file.RelativePath, file.Content, file.Sha256)),
            second.Bundle!.Files.Select(file => (file.RelativePath, file.Content, file.Sha256)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_NoOpDifference_StillDefinesAnInheritingSelectableVariant()
    {
        var noOp = Variant(
            "9e53f7b4c31042c6d2a186b23c417357",
            "al",
            null,
            "basic",
            "keyboardstudio_basic",
            "Albanian - KeyboardStudio",
            []);

        var result = XkbUserBundleGenerator.Generate([noOp]);
        var symbols = result.Bundle!.Find("symbols/keyboardstudio")!.Content;

        Assert.Contains("include \"%S/al(basic)\"", symbols);
        Assert.DoesNotContain("    key ", symbols);
        Assert.Contains("xkb_symbols \"keyboardstudio_basic\"", result.Bundle.Find("symbols/al")!.Content);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Generate_ManifestHashesEveryInstallableFileAndCarriesStableIdentities()
    {
        var bundle = XkbUserBundleGenerator.Generate([Polish()]).Bundle!;
        var manifestFile = bundle.Find("keyboardstudio-bundle.json")!;
        using var manifest = JsonDocument.Parse(manifestFile.Content);

        Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("1.0", manifest.RootElement.GetProperty("generatorVersion").GetString());
        var variant = manifest.RootElement.GetProperty("variants")[0];
        Assert.Equal(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            variant.GetProperty("projectInstallationId").GetString());
        Assert.Equal(["KeyA"], variant.GetProperty("changedKeyIds").EnumerateArray()
            .Select(value => value.GetString()));

        var manifestHashes = manifest.RootElement.GetProperty("files")
            .EnumerateArray()
            .ToDictionary(
                entry => entry.GetProperty("relativePath").GetString()!,
                entry => entry.GetProperty("sha256").GetString()!,
                StringComparer.Ordinal);
        Assert.Equal(3, manifestHashes.Count);
        foreach (var file in bundle.Files.Where(file => file != manifestFile))
        {
            Assert.Equal(file.Sha256, manifestHashes[file.RelativePath]);
            Assert.Equal(Hash(file.Content), file.Sha256);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Generate_WhenInternalSectionPrefixesCollide_ReturnsADiagnostic()
    {
        var first = Polish();
        var second = Variant(
            "7c31d5f2a19effffffffffffffffffff",
            "al",
            null,
            "basic",
            "keyboardstudio_basic",
            "Albanian - KeyboardStudio",
            []);

        var result = XkbUserBundleGenerator.Generate([first, second]);

        Assert.False(result.Success);
        Assert.Null(result.Bundle);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == XkbUserBundleGenerator.InternalSectionCollisionCode);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Generate_WhenPublicLayoutVariantPairCollides_ReturnsADiagnostic()
    {
        var second = Variant(
            "8d42e6a3b20f41b5c1f075a12b306246",
            "pl",
            null,
            "basic",
            "keyboardstudio_programmer",
            "Other Polish - KeyboardStudio",
            []);

        var result = XkbUserBundleGenerator.Generate([Polish(), second]);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == XkbUserBundleGenerator.PublicVariantCollisionCode);
    }

    private static XkbUserVariantLayout Polish() => Variant(
        "7c31d5f2a19e40a4b0ef64f01a295135",
        "pl",
        "qwertz",
        "qwertz",
        "keyboardstudio_programmer",
        "Polish – KeyboardStudio",
        [
            new XkbUserVariantKeyMapping(
                "KeyA",
                "<AC01>",
                XkbKeyType.FourLevelSemialphabetic,
                ["a", "A", "U0105", "NoSymbol"])
        ]);

    private static XkbUserVariantLayout Variant(
        string installationId,
        string baseLayoutId,
        string? baseVariantId,
        string baseSectionId,
        string publicVariantId,
        string description,
        IReadOnlyList<XkbUserVariantKeyMapping> mappings) =>
        new(
            new XkbUserVariantMetadata(
                installationId,
                baseLayoutId,
                baseVariantId,
                baseSectionId,
                publicVariantId,
                description),
            mappings,
            mappings.Any(mapping => mapping.Keysyms.Count >= 3));

    private static async Task AssertGoldenAsync(string name, string actual)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Golden",
            "UserBundle",
            "Polish",
            name);
        var expected = (await File.ReadAllTextAsync(path))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(expected, actual);
    }

    private static string Hash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
