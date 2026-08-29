using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Import, generate, re-import.
///
/// The importer and the generator are inverses, and each was tested against hand-written material
/// that the other never saw. Composing them is the only test that can catch the two disagreeing:
/// a key name written one way and read another, a level ordering that survives generation and is
/// lost on the way back, an output the generator emits in a form the importer declines. Whatever
/// the first import produced is the fixed point the second one has to return.
///
/// Anything the first import dropped is dropped before the round trip starts, so this asserts that
/// the pair is lossless over what the model holds — not that XKB survives the model.
/// </summary>
public sealed class XkbImportRoundTripTests
{
    public static TheoryData<string, string?> Layouts => new()
    {
        { "us", null },
        // Three of the four levels in use, so the round trip has to preserve level ordering rather
        // than just pairs.
        { "pl", null },
        // A different geometry, chosen by the importer rather than given to it, which the generated
        // file has to describe well enough for the second import to choose it again.
        { "de", "nodeadkeys" },
        // Keypad keys and a full four levels, which is the widest the model goes.
        { "fr", "oss" }
    };

    [Theory]
    [Trait("Category", "Golden")]
    [MemberData(nameof(Layouts))]
    public async Task Import_ThenGenerate_ThenImport_ReturnsTheSameLayout(string layoutId, string? variantId)
    {
        var source = VendoredXkbFixture.CreateSource();
        var descriptor = (await source.ListAsync()).Single(item =>
            item.LayoutId == layoutId && item.VariantId == variantId);

        var first = await source.ImportAsync(descriptor.ToReference(), LayoutImportOptions.Default);
        Assert.True(first.Success);

        var generated = Generate(first.Project!, layoutId);

        var directory = Directory.CreateTempSubdirectory("keyboardstudio-roundtrip");
        try
        {
            // The file is named after the layout because a generated file is meant to be dropped
            // into an XKB root under that name, and because the name is what an include would use
            // to reach back into it.
            var path = Path.Combine(directory.FullName, layoutId);
            await File.WriteAllTextAsync(path, generated);

            var second = await VendoredXkbFixture.CreateFileSource().ImportAsync(
                new ImportableLayoutReference(
                    XkbSymbolsFileImportSource.SourceId,
                    layoutId,
                    VariantId: null,
                    path),
                // The geometry travels with the layout rather than being inferred a second time:
                // a generated file records the keys, not the board they were laid on.
                new LayoutImportOptions(TemplateId: first.Project!.Keyboard.Id));

            Assert.True(second.Success, Describe(second));
            Assert.Equal(0, second.Report.KeysSkipped);
            AssertSameLayout(first.Project!, second.Project!);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Golden")]
    public async Task Import_ThenGenerate_ProducesAFileTheGeneratorWouldAcceptBack()
    {
        // Generation of a re-imported project has to succeed too, or the round trip is only stable
        // in one direction and a document that came from a file could not be built from.
        var source = VendoredXkbFixture.CreateSource();
        var descriptor = (await source.ListAsync()).Single(item =>
            item.LayoutId == "pl" && item.VariantId is null);
        var first = await source.ImportAsync(descriptor.ToReference(), LayoutImportOptions.Default);

        var once = Generate(first.Project!, "pl");

        var directory = Directory.CreateTempSubdirectory("keyboardstudio-roundtrip");
        try
        {
            var path = Path.Combine(directory.FullName, "pl");
            await File.WriteAllTextAsync(path, once);

            var second = await VendoredXkbFixture.CreateFileSource().ImportAsync(
                new ImportableLayoutReference(XkbSymbolsFileImportSource.SourceId, "pl", null, path),
                new LayoutImportOptions(TemplateId: first.Project!.Keyboard.Id));

            Assert.Equal(once, Generate(second.Project!, "pl"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static string Generate(KeyboardProject project, string layoutId)
    {
        var translation = new XkbLayoutTranslator().Translate(
            project,
            new XkbLayoutMetadata(layoutId, "basic", layoutId));

        Assert.True(
            translation.Success,
            string.Join(
                Environment.NewLine,
                translation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));

        return new XkbSymbolsGenerator().Generate(translation.Layout!).Content;
    }

    private static void AssertSameLayout(KeyboardProject expected, KeyboardProject actual)
    {
        Assert.Equal(expected.Keyboard.Id, actual.Keyboard.Id);

        var before = expected.Layout.Mappings.OrderBy(mapping => mapping.KeyId, StringComparer.Ordinal).ToArray();
        var after = actual.Layout.Mappings.OrderBy(mapping => mapping.KeyId, StringComparer.Ordinal).ToArray();

        Assert.Equal(
            before.Select(mapping => mapping.KeyId),
            after.Select(mapping => mapping.KeyId));

        foreach (var (left, right) in before.Zip(after))
        {
            Assert.Equal(left.LogicalKey, right.LogicalKey);

            // KeyMapping is a mutable model type rather than a record, so the layers are compared
            // one by one; the outputs themselves are records and compare by value.
            Assert.Equal(
                left.Outputs.OrderBy(entry => entry.Key),
                right.Outputs.OrderBy(entry => entry.Key));
        }
    }

    private static string Describe(LayoutImportResult result) =>
        string.Join(
            Environment.NewLine,
            result.Report.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
