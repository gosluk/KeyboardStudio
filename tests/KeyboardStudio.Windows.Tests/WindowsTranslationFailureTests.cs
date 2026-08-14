using KeyboardStudio.Core;
using KeyboardStudio.Windows;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsTranslationFailureTests
{
    [Fact]
    public void Translate_WhenCharacterIsAssignedToScanOnlyKey_ThrowsStructuredDiagnostic()
    {
        var project = CreateSingleKeyProject(LogicalKey.Enter, new CharacterOutput("x"));

        var exception = Assert.Throws<WindowsTranslationException>(
            () => WindowsLayoutTranslator.Translate(project));

        Assert.Contains(exception.Issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedCharacterMapping && issue.KeyId == "TestKey");
    }

    [Fact]
    public void Translate_WhenSpecialOutputCannotBeRepresented_ThrowsStructuredDiagnostic()
    {
        var project = CreateSingleKeyProject(LogicalKey.A, new SpecialKeyOutput(LogicalKey.Enter));

        var exception = Assert.Throws<WindowsTranslationException>(
            () => WindowsLayoutTranslator.Translate(project));

        Assert.Contains(exception.Issues, issue =>
            issue.Code == WindowsDiagnosticCodes.UnsupportedSpecialKeyMapping && issue.KeyId == "TestKey");
    }

    [Fact]
    public void Translate_WhenMappingReferencesMissingPhysicalKey_PreservesCoreDiagnostic()
    {
        var project = CreateSingleKeyProject(LogicalKey.A, new CharacterOutput("a"));
        project.Keyboard.Keys.Clear();

        var exception = Assert.Throws<WindowsTranslationException>(
            () => WindowsLayoutTranslator.Translate(project));

        Assert.Contains(exception.Issues, issue =>
            issue.Code == KeyboardProjectDiagnosticCodes.MappingReferencesMissingKey && issue.KeyId == "TestKey");
    }

    [Fact]
    public void Translate_WhenOutputIsExplicitlyUnmapped_PreservesScanCodeWithoutCharacterRow()
    {
        var project = CreateSingleKeyProject(LogicalKey.A, new NoOutput());

        var layout = WindowsLayoutTranslator.Translate(project);

        Assert.Single(layout.VscToVkMappings);
        Assert.Empty(layout.Characters.Rows);
    }

    private static KeyboardProject CreateSingleKeyProject(LogicalKey logicalKey, KeyOutput output) =>
        new()
        {
            Metadata = new ProjectMetadata
            {
                Name = "Translation failure test",
                Description = "Translation failure test",
                Version = "1.0.0",
                Language = "en-US"
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = "translation-failure-test",
                Keys = [new PhysicalKey { Id = "TestKey", ScanCode = 0x1E }]
            },
            Layout = new KeyboardLayout
            {
                Mappings =
                [
                    new KeyMapping
                    {
                        KeyId = "TestKey",
                        LogicalKey = logicalKey,
                        Outputs = { [ModifierLayer.Default] = output }
                    }
                ]
            }
        };
}
