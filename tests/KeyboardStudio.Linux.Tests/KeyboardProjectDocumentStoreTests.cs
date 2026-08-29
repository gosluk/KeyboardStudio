using System.Text;
using System.Text.Json;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using KeyboardStudio.Testing;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class KeyboardProjectDocumentStoreTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_PreservesWindowsAndLinuxProfiles()
    {
        var document = new KeyboardProjectDocument(
            TestProjectFactory.Create(),
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)
            {
                ["windows"] = new("windows", new Dictionary<string, string>
                {
                    ["layoutId"] = "kbd-demo"
                }),
                ["linuxXkb"] = new("linuxXkb", new Dictionary<string, string>
                {
                    ["layoutId"] = "demo",
                    ["sectionId"] = "basic"
                })
            });
        var store = new JsonKeyboardProjectDocumentStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(document, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Equal(document.Project.Metadata.Name, loaded.Project.Metadata.Name);
        Assert.Equal("kbd-demo", loaded.TargetProfiles["windows"].Settings["layoutId"]);
        Assert.Equal("basic", loaded.TargetProfiles["linuxXkb"].Settings["sectionId"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_PreservesImportProvenance()
    {
        var importedAt = new DateTimeOffset(2026, 8, 29, 9, 15, 0, TimeSpan.Zero);
        var document = new KeyboardProjectDocument(
            TestProjectFactory.Create(),
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal),
            new LayoutImportProvenance(
                "linux-xkb",
                "pl",
                "qwertz",
                "/usr/share/X11/xkb/symbols/pl",
                "Polish (QWERTZ)",
                importedAt));
        var store = new JsonKeyboardProjectDocumentStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(document, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Equal(document.ImportProvenance, loaded.ImportProvenance);
        Assert.Equal("pl(qwertz)", loaded.ImportProvenance!.Describe());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Save_WritesTheCurrentEnvelopeVersion()
    {
        var store = new JsonKeyboardProjectDocumentStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(
            new KeyboardProjectDocument(
                TestProjectFactory.Create(),
                new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)),
            stream);

        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        Assert.Equal(
            JsonKeyboardProjectDocumentStore.CurrentDocumentSchemaVersion,
            json.RootElement.GetProperty("documentSchemaVersion").GetInt32());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Load_ForTheEnvelopeVersionThatPredatesImport_MigratesItAndReportsNoProvenance()
    {
        // Version 1 is every document saved before import existed. It has no provenance to read,
        // and that is not a defect to report: nothing imported it.
        var store = new JsonKeyboardProjectDocumentStore();
        await using var current = new MemoryStream();
        await store.SaveAsync(
            new KeyboardProjectDocument(
                TestProjectFactory.Create(),
                new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)),
            current);

        var legacy = Encoding.UTF8.GetString(current.ToArray())
            .Replace(
                $"\"documentSchemaVersion\": {JsonKeyboardProjectDocumentStore.CurrentDocumentSchemaVersion}",
                $"\"documentSchemaVersion\": {JsonKeyboardProjectDocumentStore.FirstDocumentSchemaVersion}",
                StringComparison.Ordinal);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(legacy));
        var loaded = await store.LoadAsync(stream);

        Assert.Null(loaded.ImportProvenance);
        Assert.Equal("Demo layout", loaded.Project.Metadata.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Load_ForAnEnvelopeNewerThanThisRelease_IsRejected()
    {
        var future = $$"""
            { "documentSchemaVersion": {{JsonKeyboardProjectDocumentStore.CurrentDocumentSchemaVersion + 1}} }
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(future));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new JsonKeyboardProjectDocumentStore().LoadAsync(stream));
    }
}
