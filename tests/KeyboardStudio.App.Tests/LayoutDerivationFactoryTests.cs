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

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenTheOnlyLossIsALevelTheSourceStillDescribes_KeepsTheKeySafe()
    {
        var diagnostic = new LayoutImportDiagnostic(
            ValidationSeverity.Warning,
            LayoutImportDiagnosticCodes.LayerBeyondModelDropped,
            "Level 5 of '<AD01>' was dropped; the model holds four levels.",
            KeyId: "KeyA");

        var derivation = Create(
            diagnostic,
            new LayoutImportKeySource("KeyA", "<AD01>", ["a", "A", "aogonek", "Aogonek", "U1E9E"], "FOUR_LEVEL_PLUS_LOCK"));

        Assert.True(Assert.Single(derivation.BaselineMappings).IsSafeToOverride);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenALevelWasLostAndNoSourceCameWithIt_LeavesTheKeyUnsafe()
    {
        var diagnostic = new LayoutImportDiagnostic(
            ValidationSeverity.Warning,
            LayoutImportDiagnosticCodes.DeadKeyDropped,
            "A dead key was dropped.",
            KeyId: "KeyA");

        var derivation = Create(diagnostic);

        Assert.False(Assert.Single(derivation.BaselineMappings).IsSafeToOverride);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenTheLossIsNotALevel_LeavesTheKeyUnsafeEvenWithItsSource()
    {
        // An action is not a level, so nothing the source says about levels can restore it.
        var diagnostic = new LayoutImportDiagnostic(
            ValidationSeverity.Warning,
            LayoutImportDiagnosticCodes.UnsupportedConstructIgnored,
            "Key <AD01> used 'actions', which has no equivalent in the model; it was ignored.",
            KeyId: "KeyA");

        var derivation = Create(
            diagnostic,
            new LayoutImportKeySource("KeyA", "<AD01>", ["a", "A"], null));

        Assert.False(Assert.Single(derivation.BaselineMappings).IsSafeToOverride);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_CarriesTheSourceLevelsOntoTheBaseline()
    {
        var derivation = Create(
            new LayoutImportDiagnostic(
                ValidationSeverity.Warning,
                LayoutImportDiagnosticCodes.LayerBeyondModelDropped,
                "Level 5 was dropped.",
                KeyId: "KeyA"),
            new LayoutImportKeySource("KeyA", "<AD01>", ["a", "A", "aogonek", "Aogonek", "U1E9E"], "FOUR_LEVEL_PLUS_LOCK"));

        var mapping = Assert.Single(derivation.BaselineMappings);
        Assert.Equal(["a", "A", "aogonek", "Aogonek", "U1E9E"], mapping.SourceLevels);
        Assert.Equal("FOUR_LEVEL_PLUS_LOCK", mapping.SourceKeyType);
    }

    private static LayoutDerivation Create(
        LayoutImportDiagnostic diagnostic,
        LayoutImportKeySource? keySource = null)
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
            "qwertz",
            keySource is null ? [] : [keySource]);
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
