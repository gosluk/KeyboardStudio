using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using KeyboardStudio.Testing;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class LayoutDerivationFactoryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenLossBelongsToAnotherResolvedKey_KeepsUnrelatedMappingsSafe()
    {
        var diagnostic = new LayoutImportDiagnostic(
            ValidationSeverity.Warning,
            LayoutImportDiagnosticCodes.UnsupportedConstructIgnored,
            "Caps Lock action was ignored.",
            KeyId: "CapsLock")
        {
            SourceKeyName = "<CAPS>"
        };

        var derivation = Create(diagnostic);

        Assert.True(Assert.Single(derivation.BaselineMappings).IsSafeToOverride);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenLossIsLayoutWide_MarksEveryMappingUnsafe()
    {
        var diagnostic = new LayoutImportDiagnostic(
            ValidationSeverity.Warning,
            LayoutImportDiagnosticCodes.CompositionTargetUnavailable,
            "An included definition was unavailable.");

        var derivation = Create(diagnostic);

        Assert.False(Assert.Single(derivation.BaselineMappings).IsSafeToOverride);
    }

    private static LayoutDerivation Create(LayoutImportDiagnostic diagnostic)
    {
        var project = TestProjectFactory.Create();
        project.Layout.Mappings.Clear();
        project.Layout.Mappings.Add(new KeyMapping
        {
            KeyId = "KeyA",
            LogicalKey = LogicalKey.A,
            Outputs =
            {
                [ModifierLayer.Default] = new CharacterOutput("a")
            }
        });
        var result = LayoutImportResult.Succeeded(
            project,
            "iso-105",
            new LayoutImportReport(
                LayoutImportFidelity.Reduced,
                KeysImported: 1,
                KeysSkipped: 0,
                ResolvedIncludeChain: ["pl(qwertz)"],
                Diagnostics: [diagnostic]),
            "qwertz");
        var descriptor = new ImportableLayoutDescriptor(
            "linux-xkb",
            "pl",
            "qwertz",
            "Polish (QWERTZ)",
            null,
            [],
            ["PL"],
            LayoutSourceOrigin.System,
            "/usr/share/X11/xkb/symbols/pl");

        return Assert.IsType<LayoutDerivation>(
            LayoutDerivationFactory.Create(descriptor, result, DateTimeOffset.UnixEpoch));
    }
}
