using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

/// <summary>
/// Imports the pinned layouts and compares the whole result against a checked-in snapshot.
///
/// The unit tests around the parser and the translator each assert one rule, and between them they
/// still cannot say what importing Polish produces. That is what a golden is for: it pins every
/// decision the import made at once — the geometry it chose, the name it took, the layers it filled,
/// the levels it dropped and what it said about them — so that a change to any of those shows up as
/// a diff to read rather than as silence.
///
/// A failing golden is not automatically a defect. Run with
/// <c>KEYBOARDSTUDIO_UPDATE_GOLDEN=1</c> to rewrite the snapshots, then read the diff: it is the
/// change under review.
/// </summary>
public sealed class XkbGoldenImportTests
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Goldens are read by people. A Polish layout whose every accented character is written
        // ą is a snapshot nobody can review.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// The vendored layouts, and why each is here. They are the same set
    /// <c>scripts/vendor-xkb-fixtures.py</c> copies, and adding one means running it again.
    /// </summary>
    public static TheoryData<string, string?> Layouts => new()
    {
        // ANSI, no composition to speak of: the baseline every other import is a departure from.
        { "us", null },
        // Dead keys on the base layer, which the model cannot hold and has to report.
        { "us", "intl" },
        // A national layout as a difference over `latin`, with its alphabet on the third level.
        { "pl", null },
        // The same alphabet on a different arrangement, so the physical identity of each key can
        // be seen to survive independently of what it types.
        { "pl", "qwertz" },
        // QWERTZ with dead keys and `latin(type4)`, the deepest include chain of the four.
        { "de", null },
        // The same layout with the dead keys resolved, which is a different import of one file.
        { "de", "nodeadkeys" },
        // AZERTY: the arrangement that shares fewest positions with the seed.
        { "fr", null },
        // The variant that pulls in the keypad and no-break-space definitions.
        { "fr", "oss" }
    };

    [Theory]
    [Trait("Category", "Golden")]
    [MemberData(nameof(Layouts))]
    public async Task ImportAsync_ForAPinnedLayout_MatchesItsGoldenSnapshot(string layoutId, string? variantId)
    {
        var source = VendoredXkbFixture.CreateSource();
        var descriptors = await source.ListAsync();

        var descriptor = descriptors.FirstOrDefault(item =>
            item.LayoutId == layoutId && item.VariantId == variantId);
        Assert.True(
            descriptor is not null,
            $"'{Describe(layoutId, variantId)}' is not in the vendored registry; re-run scripts/vendor-xkb-fixtures.py.");

        var result = await source.ImportAsync(descriptor!.ToReference(), LayoutImportOptions.Default);
        Assert.True(result.Success, $"Importing '{Describe(layoutId, variantId)}' failed.");

        // An import that produced a document the editor would refuse to build is a failed import
        // whatever its report says, and the snapshot below would happily pin one.
        var validation = new KeyboardProjectValidator().Validate(result.Project!);
        Assert.True(
            validation.IsValid,
            string.Join(
                Environment.NewLine,
                validation.Issues
                    .Where(issue => issue.Severity == ValidationSeverity.Error)
                    .Select(issue => issue.Message)));

        var actual = JsonSerializer.Serialize(Snapshot(descriptor, result), SnapshotOptions) + "\n";
        var name = $"{layoutId}{(variantId is null ? "" : $"-{variantId}")}.json";

        if (UpdateRequested())
        {
            // Rewriting is a developer's gesture and its whole effect is to make this test pass.
            // On a build machine that is indistinguishable from having no goldens at all, so the
            // variable is refused there rather than obeyed: a suite that rewrites what it was
            // meant to check reports success for every change it was put in place to catch.
            Assert.False(
                IsContinuousIntegration(),
                "KEYBOARDSTUDIO_UPDATE_GOLDEN is set in CI, where rewriting the goldens would make "
                + "this suite pass for any change. Update them locally and commit the diff.");

            await File.WriteAllTextAsync(Path.Combine(SourceGoldenDirectory(), name), actual);
            return;
        }

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Golden", "Import", name);
        Assert.True(
            File.Exists(expectedPath),
            $"No golden for '{Describe(layoutId, variantId)}'. Run the suite with KEYBOARDSTUDIO_UPDATE_GOLDEN=1 to write one.");

        var expected = (await File.ReadAllTextAsync(expectedPath)).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Category", "Golden")]
    public async Task ListAsync_OverTheVendoredDatabase_OffersTheRegistrysLayoutsAndTheFilesItOmits()
    {
        // The goldens each look up one entry, so between them they say nothing about the listing
        // itself. The vendored root is small enough to state exhaustively, and it happens to hold
        // both kinds of entry: four layouts the registry describes, and the five files those
        // layouts include, which are importable but nameless.
        var descriptors = await VendoredXkbFixture.CreateSource().ListAsync();

        Assert.Equal(
            ["de", "fr", "keypad", "kpdl", "latin", "level3", "nbsp", "pl", "us"],
            descriptors.Select(descriptor => descriptor.LayoutId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        // A layout the registry describes arrives with its description, its variants, and the
        // country that decides which geometry it is imported onto.
        var polish = descriptors.Where(descriptor => descriptor.LayoutId == "pl").ToArray();
        var basic = Assert.Single(polish, descriptor => descriptor.VariantId is null);
        Assert.Equal("Polish", basic.DisplayName);
        Assert.Contains("PL", basic.Countries);
        Assert.Contains(polish, descriptor => descriptor.VariantId == "qwertz");

        // A file nothing describes is offered under its own name and nothing more, which is the
        // whole of what is known about it until someone imports it.
        foreach (var file in new[] { "keypad", "kpdl", "latin", "level3", "nbsp" })
        {
            var entry = Assert.Single(descriptors, descriptor => descriptor.LayoutId == file);
            Assert.Null(entry.VariantId);
            Assert.Equal(file, entry.DisplayName);
            Assert.Null(entry.ShortDescription);
            Assert.Empty(entry.Languages);
            Assert.Empty(entry.Countries);
        }

        Assert.All(descriptors, descriptor => Assert.Equal(LayoutSourceOrigin.System, descriptor.Origin));
    }

    private static GoldenImport Snapshot(ImportableLayoutDescriptor descriptor, LayoutImportResult result)
    {
        var project = result.Project!;

        return new GoldenImport(
            descriptor.SourceId,
            descriptor.LayoutId,
            descriptor.VariantId,
            result.SuggestedTemplateId,
            project.Keyboard.Id,
            project.Metadata.Name,
            project.Metadata.Description,
            project.Metadata.Language,
            result.Report.Fidelity,
            result.Report.KeysImported,
            result.Report.KeysSkipped,
            [.. result.Report.ResolvedIncludeChain.Select(VendoredXkbFixture.Anonymize)],
            [.. result.Report.Diagnostics.Select(diagnostic => new GoldenDiagnostic(
                diagnostic.Severity,
                diagnostic.Code,
                VendoredXkbFixture.Anonymize(diagnostic.Message),
                diagnostic.KeyId,
                diagnostic.Layer))],
            [.. project.Layout.Mappings
                .OrderBy(mapping => mapping.KeyId, StringComparer.Ordinal)
                .Select(mapping => new GoldenMapping(
                    mapping.KeyId,
                    mapping.LogicalKey,
                    Render(mapping, ModifierLayer.Default),
                    Render(mapping, ModifierLayer.Shift),
                    Render(mapping, ModifierLayer.AltGr),
                    Render(mapping, ModifierLayer.ShiftAltGr)))]);
    }

    /// <summary>
    /// One layer of one key, as a string. Characters are written as themselves so a reviewer reads
    /// the layout rather than decodes it; anything else is written in brackets so it cannot be
    /// mistaken for one.
    /// </summary>
    private static string? Render(KeyMapping mapping, ModifierLayer layer) =>
        mapping.Outputs.TryGetValue(layer, out var output)
            ? output switch
            {
                CharacterOutput character => character.Value,
                SpecialKeyOutput special => $"[{special.Key}]",
                NoOutput => "[none]",
                _ => $"[{output.GetType().Name}]"
            }
            : null;

    /// <summary>
    /// Whether the caller asked for the snapshots to be rewritten instead of compared.
    /// </summary>
    private static bool UpdateRequested() =>
        string.Equals(
            Environment.GetEnvironmentVariable("KEYBOARDSTUDIO_UPDATE_GOLDEN"),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// Whether this is a build machine. The same <c>CI</c> variable the integration suites read to
    /// decide that a missing tool is a broken image rather than a developer without it installed.
    /// </summary>
    private static bool IsContinuousIntegration() =>
        string.Equals(
            Environment.GetEnvironmentVariable("CI"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The goldens in the repository rather than the copies beside the built test assembly, so that
    /// rewriting them updates what is under review.
    /// </summary>
    private static string SourceGoldenDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "KeyboardStudio.Linux.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException(
                "The test project directory was not found above the build output, so the goldens cannot be rewritten.");
        }

        var goldens = Path.Combine(directory.FullName, "Fixtures", "Golden", "Import");
        Directory.CreateDirectory(goldens);
        return goldens;
    }

    private static string Describe(string layoutId, string? variantId) =>
        variantId is null ? layoutId : $"{layoutId}({variantId})";

    private sealed record GoldenImport(
        string SourceId,
        string LayoutId,
        string? VariantId,
        string? SuggestedTemplateId,
        string KeyboardId,
        string ProjectName,
        string ProjectDescription,
        string Language,
        LayoutImportFidelity Fidelity,
        int KeysImported,
        int KeysSkipped,
        IReadOnlyList<string> IncludeChain,
        IReadOnlyList<GoldenDiagnostic> Diagnostics,
        IReadOnlyList<GoldenMapping> Mappings);

    private sealed record GoldenDiagnostic(
        ValidationSeverity Severity,
        string Code,
        string Message,
        string? KeyId,
        ModifierLayer? Layer);

    private sealed record GoldenMapping(
        string KeyId,
        LogicalKey LogicalKey,
        string? Default,
        string? Shift,
        string? AltGr,
        string? ShiftAltGr);
}
