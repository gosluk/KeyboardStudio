using KeyboardStudio.App;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class MvpEndToEndScenarioTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Iso105Project_EditSaveReopenValidateAndBuildLinux_PreservesCompleteWorkflow()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-e2e-{Guid.NewGuid():N}");
        var projectPath = Path.Combine(testRoot, "scenario.kbdproj");
        var outputPath = Path.Combine(testRoot, "output");
        Directory.CreateDirectory(testRoot);

        try
        {
            var saveInteraction = new ScenarioInteractionService { SavePath = projectPath };

            // This scenario edits both target profiles, so it runs with the developer override that
            // reveals the Windows target. The shipped Linux-only policy is covered separately.
            var source = TestMainWindow.WithAllBuildTargets(saveInteraction);
            Assert.Equal("iso-105", source.SelectedTemplate.Id);
            Assert.True(source.Editor.SelectKey("KeyA"));
            source.Editor.SelectedLogicalKey = LogicalKey.A;
            source.Editor.LayerMappings.Single(layer => layer.Layer == ModifierLayer.Default).Output = "a";
            source.Editor.LayerMappings.Single(layer => layer.Layer == ModifierLayer.Shift).Output = "A";
            source.Editor.LayerMappings.Single(layer => layer.Layer == ModifierLayer.AltGr).Output = "ą";
            source.Editor.LayerMappings.Single(layer => layer.Layer == ModifierLayer.ShiftAltGr).Output = "Ą";

            source.Build.ProfileSettings.Single(setting => setting.Key == BuildProfileKeys.LayoutId).Value =
                "kbdscenario";
            source.Build.SelectedTarget = source.Build.Targets.Single(
                target => target.Target == BuildTarget.LinuxXkb);
            source.Build.ProfileSettings.Single(setting => setting.Key == BuildProfileKeys.LayoutId).Value =
                "scenario";
            source.Build.ProfileSettings.Single(setting => setting.Key == BuildProfileKeys.SectionId).Value =
                "basic";

            await source.SaveAsCommand.ExecuteAsync(null);
            Assert.False(source.IsDirty);

            var openInteraction = new ScenarioInteractionService { OpenPath = projectPath };
            var reopened = TestMainWindow.WithAllBuildTargets(openInteraction);
            await reopened.OpenCommand.ExecuteAsync(null);

            Assert.Equal("iso-105", reopened.Project.Keyboard.Id);
            var mapping = reopened.Project.Layout.Find("KeyA");
            Assert.NotNull(mapping);
            Assert.Equal("ą", Assert.IsType<CharacterOutput>(mapping.Outputs[ModifierLayer.AltGr]).Value);
            Assert.False(reopened.Diagnostics.HasErrors);

            reopened.Build.SelectedTarget = reopened.Build.Targets.Single(
                target => target.Target == BuildTarget.WindowsX64);
            Assert.Equal(
                "kbdscenario",
                reopened.Build.ProfileSettings.Single(
                    setting => setting.Key == BuildProfileKeys.LayoutId).Value);

            reopened.Build.SelectedTarget = reopened.Build.Targets.Single(
                target => target.Target == BuildTarget.LinuxXkb);
            Assert.Equal(
                "scenario",
                reopened.Build.ProfileSettings.Single(
                    setting => setting.Key == BuildProfileKeys.LayoutId).Value);
            reopened.Build.OutputDirectory = outputPath;

            await reopened.Build.BuildCommand.ExecuteAsync(null);

            Assert.True(reopened.Build.HasArtifact);
            Assert.Equal(
                Path.Combine(outputPath, "xkb", "symbols", "scenario"),
                reopened.Build.ArtifactPath);
            Assert.True(File.Exists(reopened.Build.ArtifactPath));
            var symbols = await File.ReadAllTextAsync(reopened.Build.ArtifactPath);
            Assert.Contains("key <AC01>", symbols, StringComparison.Ordinal);
            Assert.Contains(
                "symbols[Group1] = [ a, A, U0105, U0104 ]",
                symbols,
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private sealed class ScenarioInteractionService : IProjectInteractionService
    {
        public string? OpenPath { get; init; }

        public string? SavePath { get; init; }

        public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
            Task.FromResult(ProjectReplacementChoice.Discard);

        public Task<string?> SelectOpenPathAsync() => Task.FromResult(OpenPath);

        public Task<string?> SelectSavePathAsync(string suggestedFileName) => Task.FromResult(SavePath);

        public Task<bool> ShowLayoutImportAsync(LayoutImportViewModel viewModel) =>
            Task.FromResult(false);

        public Task<string?> SelectSymbolsFilePathAsync() => Task.FromResult<string?>(null);

        public Task ShowErrorAsync(string title, string message) =>
            throw new Xunit.Sdk.XunitException($"{title}: {message}");
    }
}
