using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbRegistryDocumentMergerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Upsert_ExistingRegistry_PreservesUnknownContentAndDoesNotResolveExternalDtd()
    {
        const string existing = """
            <?xml version="1.0"?>
            <!DOCTYPE xkbConfigRegistry SYSTEM "file:///does/not/exist/xkb.dtd">
            <xkbConfigRegistry version="1.1">
              <!-- user's comment -->
              <layoutList>
                <layout>
                  <configItem><name>us</name><description>English</description></configItem>
                  <unknown value="keep" />
                </layout>
              </layoutList>
              <optionList><group><configItem><name>custom</name></configItem></group></optionList>
            </xkbConfigRegistry>
            """;

        var result = XkbRegistryDocumentMerger.Upsert(existing, Metadata(), null);

        Assert.True(result.Success);
        Assert.Contains("user's comment", result.Content);
        Assert.Contains("<unknown value=\"keep\"", result.Content);
        Assert.Contains("<optionList>", result.Content);
        Assert.Contains("<name>keyboardstudio_programmer</name>", result.Content);
        Assert.NotNull(result.EntrySha256);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UpsertAndRemove_MultipleProjects_PreservesTheOtherEntry()
    {
        var first = XkbRegistryDocumentMerger.Upsert(null, Metadata(), null);
        var secondMetadata = Metadata(
            "8d42e6a3b20f41b5c1f075a12b306246",
            "keyboardstudio_dvorak",
            "Polish Dvorak - KeyboardStudio");
        var second = XkbRegistryDocumentMerger.Upsert(first.Content, secondMetadata, null);

        var removed = XkbRegistryDocumentMerger.Remove(
            second.Content!,
            Metadata(),
            first.EntrySha256!);

        Assert.True(removed.Success);
        Assert.DoesNotContain("keyboardstudio_programmer", removed.Content);
        Assert.Contains("keyboardstudio_dvorak", removed.Content);
        var last = XkbRegistryDocumentMerger.Remove(
            removed.Content!,
            secondMetadata,
            second.EntrySha256!);
        Assert.Null(last.Content);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Upsert_ExistingOwnedEntry_UpdatesItWithExpectedHash()
    {
        var initial = XkbRegistryDocumentMerger.Upsert(null, Metadata(), null);
        var renamed = Metadata(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "keyboardstudio_programmer",
            "Polish Programmer - KeyboardStudio");

        var result = XkbRegistryDocumentMerger.Upsert(
            initial.Content,
            renamed,
            initial.EntrySha256);

        Assert.True(result.Success);
        Assert.Contains("Polish Programmer - KeyboardStudio", result.Content);
        Assert.DoesNotContain(">Polish - KeyboardStudio<", result.Content);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Upsert_WhenVariantIsUnowned_RefusesCollision()
    {
        const string existing = """
            <xkbConfigRegistry version="1.1"><layoutList><layout>
              <configItem><name>pl</name></configItem><variantList><variant><configItem>
                <name>keyboardstudio_programmer</name><description>Mine</description>
              </configItem></variant></variantList>
            </layout></layoutList></xkbConfigRegistry>
            """;

        var result = XkbRegistryDocumentMerger.Upsert(existing, Metadata(), null);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSR002");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Upsert_WhenOwnedEntryWasExternallyChanged_RefusesReplacement()
    {
        var initial = XkbRegistryDocumentMerger.Upsert(null, Metadata(), null);
        var changed = initial.Content!.Replace(
            "Polish - KeyboardStudio",
            "Externally renamed",
            StringComparison.Ordinal);

        var result = XkbRegistryDocumentMerger.Upsert(
            changed,
            Metadata(),
            initial.EntrySha256);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSR003");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Upsert_WhenXmlIsMalformed_ReturnsStructuredDiagnostic()
    {
        var result = XkbRegistryDocumentMerger.Upsert("<xkbConfigRegistry>", Metadata(), null);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSR001");
    }

    private static XkbUserVariantMetadata Metadata(
        string id = "7c31d5f2a19e40a4b0ef64f01a295135",
        string variant = "keyboardstudio_programmer",
        string description = "Polish - KeyboardStudio") =>
        new(id, "pl", "qwertz", "qwertz", variant, description);
}
