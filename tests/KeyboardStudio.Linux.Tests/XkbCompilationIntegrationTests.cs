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

    /// <summary>
    /// The German ß key has five levels and a type that reaches the fifth through Lock. Overriding
    /// what it types used to rewrite it as the four levels the editor holds, so the ẞ on
    /// Lock+ß quietly stopped existing. The compiler is the only witness that settles it: this
    /// asserts against the keymap xkbcommon actually builds, not against what was generated.
    /// </summary>
    [Fact]
    [Trait("Category", "XkbIntegration")]
    public async Task UserVariant_OverridingAKeyWithAFifthLevel_CompilesWithThatLevelIntact()
    {
        var executable = new PathXkbCliLocator().Find();
        if (!OperatingSystem.IsLinux() || executable is null)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail("xkbcli is required for XkbIntegration tests in Linux CI.");
            }

            return;
        }

        var source = VendoredXkbFixture.CreateSource();
        var descriptor = (await source.ListAsync()).Single(item =>
            item.LayoutId == "de" && item.VariantId == "nodeadkeys");
        var imported = await source.ImportAsync(descriptor.ToReference(), LayoutImportOptions.Default);
        Assert.True(imported.Success);

        var sources = imported.KeySources.ToDictionary(item => item.KeyId, StringComparer.Ordinal);
        Assert.Equal(
            ["ssharp", "question", "backslash", "questiondown", "U1E9E"],
            sources["Minus"].Levels);
        Assert.Equal("FOUR_LEVEL_PLUS_LOCK", sources["Minus"].KeyType);

        var baseline = imported.Project!.Layout.Mappings
            .Select(mapping => KeyMappingSnapshot.From(
                mapping,
                isSafeToOverride: true,
                sources.GetValueOrDefault(mapping.KeyId)))
            .ToArray();

        // What the user does: this key types a minus now, and nothing on the AltGr levels.
        var minus = imported.Project.Layout.Find("Minus")!;
        minus.Outputs[ModifierLayer.Default] = new CharacterOutput("-");
        minus.Outputs[ModifierLayer.Shift] = new CharacterOutput("_");
        minus.Outputs.Remove(ModifierLayer.AltGr);
        minus.Outputs.Remove(ModifierLayer.ShiftAltGr);

        var metadata = new XkbUserVariantMetadata(
            "3f2a19e40a4b0ef64f01a2951357c31d",
            "de",
            "nodeadkeys",
            "nodeadkeys",
            "keyboardstudio_fifthlevel",
            "Fifth level - KeyboardStudio");
        var translation = new XkbUserVariantTranslator().Translate(
            imported.Project,
            baseline,
            metadata);
        Assert.True(
            translation.Success,
            string.Join("; ", translation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        var bundle = XkbUserBundleGenerator.Generate([translation.Layout!]).Bundle!;

        var root = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestResults",
            "xkb-integration",
            "user-variant-fifth-level");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var write = await new XkbUserBundleWriter().WriteAsync(bundle, root);
        var compiled = await new ProcessRunner().RunAsync(new ProcessRequest(
            executable,
            [
                "compile-keymap",
                "--include", write.BundleRoot,
                "--include-defaults",
                "--layout", "de",
                "--variant", metadata.PublicVariantId
            ],
            write.BundleRoot,
            new Dictionary<string, string?>()));

        Assert.True(compiled.ExitCode == 0, compiled.StandardError);
        var key = KeymapKey(compiled.StandardOutput, "<AE11>");
        Assert.Contains("FOUR_LEVEL_PLUS_LOCK", key, StringComparison.Ordinal);
        Assert.Contains("minus", key, StringComparison.Ordinal);
        Assert.Contains("underscore", key, StringComparison.Ordinal);
        Assert.Contains("U1E9E", key, StringComparison.Ordinal);
        Assert.DoesNotContain("ssharp", key, StringComparison.Ordinal);
    }

    /// <summary>Reads one key's block out of a compiled keymap.</summary>
    private static string KeymapKey(string keymap, string keyName)
    {
        var start = keymap.IndexOf($"key {keyName}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The compiled keymap defines no {keyName}.");
        var end = keymap.IndexOf("};", start, StringComparison.Ordinal);
        return end < 0 ? keymap[start..] : keymap[start..end];
    }

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
