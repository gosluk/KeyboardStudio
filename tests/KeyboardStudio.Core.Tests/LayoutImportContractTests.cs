using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class LayoutImportContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ToReference_ForACatalogEntry_CarriesEveryIdentifyingField()
    {
        var descriptor = CreateDescriptor("linux-xkb", "pl", "qwertz");

        var reference = descriptor.ToReference();

        Assert.Equal("linux-xkb", reference.SourceId);
        Assert.Equal("pl", reference.LayoutId);
        Assert.Equal("qwertz", reference.VariantId);
        Assert.Equal("/usr/share/X11/xkb/symbols/pl", reference.SourceLocation);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToReference_ForADefaultVariant_KeepsTheVariantUnset()
    {
        var reference = CreateDescriptor("linux-xkb", "us", variantId: null).ToReference();

        Assert.Null(reference.VariantId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_WhenNothingWasDropped_ReportsExactFidelity()
    {
        var fidelity = LayoutImportReport.Classify(keysSkipped: 0, []);

        Assert.Equal(LayoutImportFidelity.Exact, fidelity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_WhenOnlyInformationalFindingsWereRaised_StillReportsExactFidelity()
    {
        // Info findings describe what the reader passed over, not what the user lost. Grading them
        // as loss would mark almost every real-world import as degraded and make the badge useless.
        var fidelity = LayoutImportReport.Classify(
            keysSkipped: 0,
            [
                new LayoutImportDiagnostic(
                    ValidationSeverity.Info,
                    LayoutImportDiagnosticCodes.UnrecognizedStatementSkipped,
                    "Skipped an unrecognized statement.")
            ]);

        Assert.Equal(LayoutImportFidelity.Exact, fidelity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_WhenAnOutputWasDroppedButEveryKeySurvived_ReportsReducedFidelity()
    {
        var fidelity = LayoutImportReport.Classify(
            keysSkipped: 0,
            [
                new LayoutImportDiagnostic(
                    ValidationSeverity.Warning,
                    LayoutImportDiagnosticCodes.DeadKeyDropped,
                    "Dropped a dead key.",
                    "BracketLeft",
                    ModifierLayer.Default)
            ]);

        Assert.Equal(LayoutImportFidelity.Reduced, fidelity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Classify_WhenAKeyWasSkipped_ReportsPartialFidelityRegardlessOfSeverity()
    {
        var fidelity = LayoutImportReport.Classify(
            keysSkipped: 1,
            [
                new LayoutImportDiagnostic(
                    ValidationSeverity.Info,
                    LayoutImportDiagnosticCodes.PhysicalKeyNotInTemplate,
                    "Skipped a key the template does not have.")
            ]);

        Assert.Equal(LayoutImportFidelity.Partial, fidelity);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Classify_WhenTheSkippedCountIsNegative_RejectsTheCall()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => LayoutImportReport.Classify(keysSkipped: -1, []));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Succeeded_ForAnImportedProject_CarriesTheProjectAndItsSuggestedTemplate()
    {
        var project = CreateProject();
        var report = CreateReport();

        var result = LayoutImportResult.Succeeded(project, "iso-105", report);

        Assert.True(result.Success);
        Assert.Same(project, result.Project);
        Assert.Equal("iso-105", result.SuggestedTemplateId);
        Assert.Same(report, result.Report);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public void Failed_ForAnImportThatProducedNothing_StillCarriesTheExplanation()
    {
        var report = new LayoutImportReport(
            LayoutImportFidelity.Partial,
            KeysImported: 0,
            KeysSkipped: 0,
            [],
            [
                new LayoutImportDiagnostic(
                    ValidationSeverity.Error,
                    LayoutImportDiagnosticCodes.CompositionDepthExceeded,
                    "The definition nests too deeply.")
            ]);

        var result = LayoutImportResult.Failed(report);

        Assert.False(result.Success);
        Assert.Null(result.Project);
        Assert.Null(result.SuggestedTemplateId);
        Assert.Same(report, result.Report);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DiagnosticCodes_AreUniqueAndUseTheImportPrefix()
    {
        // A reused number would silently merge two unrelated findings in the diagnostics list, and
        // docs/DIAGNOSTICS.md promises meanings are never reassigned.
        var codes = typeof(LayoutImportDiagnosticCodes)
            .GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(codes);
        Assert.All(codes, code => Assert.StartsWith("KSI", code, StringComparison.Ordinal));
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    private static ImportableLayoutDescriptor CreateDescriptor(
        string sourceId,
        string layoutId,
        string? variantId) =>
        new(sourceId,
            layoutId,
            variantId,
            "Polish (QWERTZ)",
            "pl",
            ["pol"],
            ["PL"],
            LayoutSourceOrigin.System,
            "/usr/share/X11/xkb/symbols/pl");

    private static LayoutImportReport CreateReport() =>
        new(LayoutImportFidelity.Exact, KeysImported: 105, KeysSkipped: 0, [], []);

    private static KeyboardProject CreateProject() =>
        new()
        {
            Metadata = new ProjectMetadata { Name = "Imported" },
            Keyboard = new PhysicalKeyboard { Id = "iso-105" },
            Layout = new KeyboardLayout()
        };
}
