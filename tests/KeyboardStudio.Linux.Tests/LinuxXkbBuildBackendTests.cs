using System.Text.Json;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class LinuxXkbBuildBackendTests
{
    [Fact]
    public void GetStatus_WhenOptionalVerifierIsMissing_KeepsGenerationAvailable()
    {
        var backend = new LinuxXkbBuildBackend(
            new XkbLayoutMetadata("demo", "basic", "Demo"),
            cliLocator: new MissingCliLocator());

        var status = backend.GetStatus(BuildTarget.LinuxXkb);

        Assert.True(status.Available);
        Assert.Contains(status.Diagnostics, diagnostic => diagnostic.Code == "KSL004");
        Assert.Contains("optional", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildAsync_WritesSymbolsArtifactAndManifestWithoutNativeCompiler()
    {
        var output = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-Xkb-{Guid.NewGuid():N}");
        try
        {
            var project = CreateProject();
            var backend = new LinuxXkbBuildBackend(
                new XkbLayoutMetadata("demo", "basic", "Demo"),
                timeProvider: new FixedTimeProvider(DateTimeOffset.UnixEpoch));
            var orchestrator = new BuildOrchestrator(
                new KeyboardProjectValidator(),
                new BuildBackendResolver([backend]));
            var stages = new List<BuildStageProgress>();

            var result = await orchestrator.BuildAsync(
                project,
                new BuildOptions(BuildTarget.LinuxXkb, output),
                new RecordingProgress(stages));

            Assert.True(result.Success);
            Assert.NotNull(result.Artifact);
            Assert.True(File.Exists(result.Artifact.ArtifactPath));
            Assert.True(File.Exists(result.Artifact.ManifestPath));
            Assert.Null(result.Compilation);
            var details = Assert.IsType<XkbBuildDetails>(result.Artifact.BackendDetails);
            var generatedFile = Assert.Single(result.Artifact.GeneratedFiles!);
            Assert.Equal(details.GeneratedSymbols.Content, generatedFile.Content);
            Assert.Equal(BuildTarget.LinuxXkb, details.Manifest.Target);
            Assert.Equal(DateTimeOffset.UnixEpoch, details.Manifest.BuildTimestampUtc);
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(details.ManifestPath));
            Assert.Equal("demo", json.RootElement.GetProperty("layoutId").GetString());
            Assert.DoesNotContain("1970", await File.ReadAllTextAsync(result.Artifact.ArtifactPath!));
            Assert.Equal(
                [
                    BuildStageNames.Validating,
                    BuildStageNames.GeneratingXkb,
                    BuildStageNames.WritingArtifact,
                    BuildStageNames.Verifying,
                    BuildStageNames.Completed
                ],
                stages.Select(stage => stage.Name).Distinct());
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static KeyboardProject CreateProject() => new()
    {
        Metadata = new ProjectMetadata
        {
            Name = "Demo",
            Description = "Demo",
            Version = "1.0.0",
            Language = "und"
        },
        Keyboard = new PhysicalKeyboard
        {
            Id = "ansi-104",
            Keys = [new PhysicalKey { Id = "KeyA", ScanCode = 30 }]
        },
        Layout = new KeyboardLayout
        {
            Mappings =
            [
                new KeyMapping
                {
                    KeyId = "KeyA",
                    LogicalKey = LogicalKey.A,
                    Outputs =
                    {
                        [ModifierLayer.Default] = new CharacterOutput("a"),
                        [ModifierLayer.Shift] = new CharacterOutput("A")
                    }
                }
            ]
        }
    };

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }

    private sealed class RecordingProgress(List<BuildStageProgress> stages) : IProgress<BuildStageProgress>
    {
        public void Report(BuildStageProgress value) => stages.Add(value);
    }

    private sealed class MissingCliLocator : IXkbCliLocator
    {
        public string? Find() => null;
    }
}
