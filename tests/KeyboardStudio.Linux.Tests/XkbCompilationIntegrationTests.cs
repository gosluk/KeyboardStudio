using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbCompilationIntegrationTests
{
    [Fact]
    [Trait("Category", "XkbIntegration")]
    public Task BuildAsync_IsoAltGrLayout_CompilesWithXkbCli() =>
        VerifyAsync("iso-altgr", CreateIsoAltGrProject());

    [Fact]
    [Trait("Category", "XkbIntegration")]
    public Task BuildAsync_AnsiTwoLevelLayout_CompilesWithXkbCli() =>
        VerifyAsync("ansi-two-level", CreateAnsiTwoLevelProject());

    private static async Task VerifyAsync(string layoutId, KeyboardProject project)
    {
        if (!OperatingSystem.IsLinux() || new PathXkbCliLocator().Find() is null)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail("xkbcli is required for XkbIntegration tests in Linux CI.");
            }

            return;
        }

        var output = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestResults",
            "xkb-integration",
            layoutId);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        var success = false;
        try
        {
            var backend = new LinuxXkbBuildBackend(
                new XkbLayoutMetadata(layoutId, "basic", layoutId),
                requireExternalVerification: true);
            var orchestrator = new BuildOrchestrator(
                new KeyboardProjectValidator(),
                new BuildBackendResolver([backend]));

            var result = await orchestrator.BuildAsync(
                project,
                new BuildOptions(BuildTarget.LinuxXkb, output));

            var details = Assert.IsType<XkbBuildDetails>(result.Artifact?.BackendDetails);
            Assert.True(
                result.Success,
                $"{string.Join(Environment.NewLine, result.Artifact!.Diagnostics.Select(item => item.Message))}{Environment.NewLine}{details.Verification.StandardError}");
            Assert.Equal(XkbVerificationStatus.Verified, details.Verification.Status);
            Assert.True(File.Exists(result.Artifact!.ArtifactPath));
            Assert.True(File.Exists(details.Verification.LogPath));
            success = true;
        }
        finally
        {
            if (success && Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    private static KeyboardProject CreateIsoAltGrProject() => CreateProject(
        "iso-105",
        new KeyMapping
        {
            KeyId = "KeyA",
            LogicalKey = LogicalKey.A,
            Outputs =
            {
                [ModifierLayer.Default] = new CharacterOutput("a"),
                [ModifierLayer.Shift] = new CharacterOutput("A"),
                [ModifierLayer.AltGr] = new CharacterOutput("ą"),
                [ModifierLayer.ShiftAltGr] = new CharacterOutput("Ą")
            }
        });

    private static KeyboardProject CreateAnsiTwoLevelProject() => CreateProject(
        "ansi-104",
        new KeyMapping
        {
            KeyId = "Slash",
            LogicalKey = LogicalKey.Slash,
            Outputs =
            {
                [ModifierLayer.Default] = new CharacterOutput("/"),
                [ModifierLayer.Shift] = new CharacterOutput("?")
            }
        },
        new KeyMapping
        {
            KeyId = "Enter",
            LogicalKey = LogicalKey.Enter
        });

    private static KeyboardProject CreateProject(string templateId, params KeyMapping[] mappings)
    {
        var keyboard = new KeyboardTemplateProvider().Load(templateId);
        return new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = "XKB integration fixture",
                Description = "Compiled by xkbcli without activating the layout.",
                Version = "1.0.0",
                Language = "und"
            },
            Keyboard = keyboard,
            Layout = new KeyboardLayout { Mappings = [.. mappings] }
        };
    }
}
