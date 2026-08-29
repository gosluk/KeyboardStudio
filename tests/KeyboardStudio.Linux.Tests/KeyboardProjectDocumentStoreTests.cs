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
    public async Task SaveAndLoad_PreservesTheExactLayoutDerivation()
    {
        var importedAt = new DateTimeOffset(2026, 8, 29, 9, 15, 0, TimeSpan.Zero);
        var derivation = new LayoutDerivation(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "linux-xkb",
            LayoutSourceOrigin.System,
            "pl",
            "qwertz",
            "qwertz",
            importedAt,
            LayoutImportFidelity.Reduced,
            [
                new KeyMappingSnapshot(
                    "KeyA",
                    LogicalKey.A,
                    new Dictionary<ModifierLayer, KeyOutput>
                    {
                        [ModifierLayer.Default] = new CharacterOutput("a"),
                        [ModifierLayer.Shift] = new CharacterOutput("A"),
                        [ModifierLayer.AltGr] = new NoOutput()
                    },
                    isSafeToOverride: false)
            ],
            "sha256:source",
            "sha256:includes");
        var document = new KeyboardProjectDocument(
            TestProjectFactory.Create(),
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal),
            LayoutDerivation: derivation);
        var store = new JsonKeyboardProjectDocumentStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(document, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        var actual = Assert.IsType<LayoutDerivation>(loaded.LayoutDerivation);
        Assert.Equal(derivation.ProjectInstallationId, actual.ProjectInstallationId);
        Assert.Equal(derivation.SourceId, actual.SourceId);
        Assert.Equal(derivation.SourceOrigin, actual.SourceOrigin);
        Assert.Equal(derivation.BaseLayoutId, actual.BaseLayoutId);
        Assert.Equal(derivation.BaseVariantId, actual.BaseVariantId);
        Assert.Equal(derivation.ResolvedBaseSectionId, actual.ResolvedBaseSectionId);
        Assert.Equal(derivation.ImportedAtUtc, actual.ImportedAtUtc);
        Assert.Equal(derivation.ImportFidelity, actual.ImportFidelity);
        Assert.Equal(derivation.SourceFingerprint, actual.SourceFingerprint);
        Assert.Equal(derivation.IncludeChainFingerprint, actual.IncludeChainFingerprint);
        var mapping = Assert.Single(actual.BaselineMappings);
        Assert.Equal("KeyA", mapping.KeyId);
        Assert.Equal(LogicalKey.A, mapping.LogicalKey);
        Assert.False(mapping.IsSafeToOverride);
        Assert.Equal(new CharacterOutput("a"), mapping.Outputs[ModifierLayer.Default]);
        Assert.Equal(new CharacterOutput("A"), mapping.Outputs[ModifierLayer.Shift]);
        Assert.IsType<NoOutput>(mapping.Outputs[ModifierLayer.AltGr]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LayoutDerivation_CopiesTheBaselineInsteadOfRetainingMutableCollections()
    {
        var outputs = new Dictionary<ModifierLayer, KeyOutput>
        {
            [ModifierLayer.Default] = new CharacterOutput("a")
        };
        var mappings = new List<KeyMappingSnapshot>
        {
            new("KeyA", LogicalKey.A, outputs)
        };

        var derivation = new LayoutDerivation(
            "7c31d5f2a19e40a4b0ef64f01a295135",
            "linux-xkb",
            LayoutSourceOrigin.System,
            "pl",
            null,
            "basic",
            DateTimeOffset.UtcNow,
            LayoutImportFidelity.Exact,
            mappings);

        outputs[ModifierLayer.Default] = new CharacterOutput("z");
        mappings.Clear();

        var mapping = Assert.Single(derivation.BaselineMappings);
        Assert.Equal(new CharacterOutput("a"), mapping.Outputs[ModifierLayer.Default]);
        Assert.IsAssignableFrom<IReadOnlyList<KeyMappingSnapshot>>(derivation.BaselineMappings);
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
    public async Task Load_ForVersionTwoImportedDocument_DoesNotInventADerivationBaseline()
    {
        var store = new JsonKeyboardProjectDocumentStore();
        await using var current = new MemoryStream();
        await store.SaveAsync(
            new KeyboardProjectDocument(
                TestProjectFactory.Create(),
                new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal),
                new LayoutImportProvenance(
                    "linux-xkb",
                    "pl",
                    "qwertz",
                    "/usr/share/X11/xkb/symbols/pl",
                    "Polish (QWERTZ)",
                    DateTimeOffset.UtcNow)),
            current);

        var versionTwo = Encoding.UTF8.GetString(current.ToArray())
            .Replace(
                $"\"documentSchemaVersion\": {JsonKeyboardProjectDocumentStore.CurrentDocumentSchemaVersion}",
                "\"documentSchemaVersion\": 2",
                StringComparison.Ordinal);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(versionTwo));

        var loaded = await store.LoadAsync(stream);

        Assert.NotNull(loaded.ImportProvenance);
        Assert.Null(loaded.LayoutDerivation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAsCopy_PreservesTheStableInstallationIdentity()
    {
        var document = new KeyboardProjectDocument(
            TestProjectFactory.Create(),
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal),
            LayoutDerivation: new LayoutDerivation(
                "7c31d5f2a19e40a4b0ef64f01a295135",
                "linux-xkb",
                LayoutSourceOrigin.System,
                "al",
                null,
                "basic",
                DateTimeOffset.UtcNow,
                LayoutImportFidelity.Exact,
                []));
        var store = new JsonKeyboardProjectDocumentStore();
        await using var first = new MemoryStream();
        await store.SaveAsync(document, first);
        first.Position = 0;
        var loaded = await store.LoadAsync(first);
        await using var copy = new MemoryStream();

        await store.SaveAsync(loaded, copy);
        copy.Position = 0;
        var copied = await store.LoadAsync(copy);

        Assert.Equal(
            document.LayoutDerivation!.ProjectInstallationId,
            copied.LayoutDerivation!.ProjectInstallationId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAndLoad_AuthoredDocumentHasNoDerivation()
    {
        var store = new JsonKeyboardProjectDocumentStore();
        await using var stream = new MemoryStream();
        await store.SaveAsync(
            new KeyboardProjectDocument(
                TestProjectFactory.Create(),
                new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)),
            stream);

        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Null(loaded.LayoutDerivation);
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
