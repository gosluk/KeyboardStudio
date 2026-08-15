using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class WindowsNativeCompilationIntegrationTests
{
    public static TheoryData<string> RepresentativeFixtures => new()
    {
        "us-like-letters",
        "altgr-unicode",
        "iso-physical-layout",
        "special-extended-keys"
    };

    [Theory]
    [Trait("Category", "Unit")]
    [MemberData(nameof(RepresentativeFixtures))]
    public async Task GenerateAsync_RepresentativeFixture_ProducesCompleteNativeSource(
        string fixtureName)
    {
        var (project, metadata) = CreateFixture(fixtureName);

        var validation = new KeyboardProjectValidator().Validate(project);
        var artifact = await new WindowsArtifactGenerator(metadata).GenerateAsync(
            project,
            new BuildOptions(BuildTarget.WindowsX64, "out"));

        Assert.False(validation.HasErrors);
        Assert.Equal(
            ["keyboard.c", "keyboard.def", "keyboard.h", "keyboard.rc"],
            artifact.Source.Files.Keys);
        Assert.Contains("KbdLayerDescriptor", artifact.Source.Files["keyboard.c"]);
        Assert.Contains("KbdLayerDescriptor @1", artifact.Source.Files["keyboard.def"]);
    }

    [Theory]
    [Trait("Category", "WindowsIntegration")]
    [MemberData(nameof(RepresentativeFixtures))]
    public async Task BuildAsync_RepresentativeFixture_ProducesVerifiedReproducibleDll(
        string fixtureName)
    {
        var environment = new WindowsBuildEnvironment();
        if (!environment.CanBuild(BuildTarget.WindowsX64))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("CI"),
                    "true",
                    StringComparison.OrdinalIgnoreCase) &&
                OperatingSystem.IsWindows())
            {
                Assert.Fail(environment.GetStatus(BuildTarget.WindowsX64).Message);
            }

            return;
        }

        var buildRoot = Path.Combine(
            Directory.GetCurrentDirectory(),
            "TestResults",
            "windows-integration",
            fixtureName);
        if (Directory.Exists(buildRoot))
        {
            Directory.Delete(buildRoot, recursive: true);
        }

        var success = false;
        try
        {
            var (project, metadata) = CreateFixture(fixtureName);
            var generator = new WindowsArtifactGenerator(metadata);
            var options = new BuildOptions(
                BuildTarget.WindowsX64,
                buildRoot,
                CleanupPolicy: BuildCleanupPolicy.KeepAll,
                VerifyReproducibility: true);
            var orchestrator = new BuildOrchestrator(
                new KeyboardProjectValidator(),
                new BuildBackendResolver([
                    new WindowsBuildBackend(
                        generator,
                        environment,
                        new MsvcKeyboardCompiler(environment, new ProcessRunner()))
                ]));

            var result = await orchestrator.BuildAsync(project, options);

            var compilation = Assert.IsType<CompilationResult>(result.Compilation);
            Assert.True(result.Success, $"Fixture: {fixtureName}{Environment.NewLine}{compilation.RawLog}");
            Assert.True(File.Exists(compilation.ArtifactPath), compilation.RawLog);
            Assert.True(File.Exists(compilation.ManifestPath), compilation.RawLog);
            var verification = Assert.IsType<ArtifactVerificationResult>(compilation.Verification);
            Assert.True(verification.IsDll, compilation.RawLog);
            Assert.True(verification.ExpectedExportFound, compilation.RawLog);
            Assert.Equal(ArtifactLoadTestStatus.Passed, verification.LoadTest.Status);
            Assert.True(result.Reproducibility?.Success is true, compilation.RawLog);
            success = true;
        }
        finally
        {
            if (success && Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }

    private static (KeyboardProject Project, WindowsLayoutMetadata Metadata) CreateFixture(
        string fixtureName) => fixtureName switch
    {
        "us-like-letters" => (
            CreateProject(
                "ansi-104",
                "Windows CI US-like letters",
                CharacterMapping("KeyQ", LogicalKey.Q, "q", "Q"),
                CharacterMapping("KeyA", LogicalKey.A, "a", "A"),
                CharacterMapping("KeyZ", LogicalKey.Z, "z", "Z")),
            new WindowsLayoutMetadata("kbd-ci-us", "Windows CI US-like letters")),
        "altgr-unicode" => (
            CreateProject(
                "ansi-104",
                "Windows CI AltGr Unicode",
                CharacterMapping("KeyA", LogicalKey.A, "a", "A", "ą", "Ą"),
                CharacterMapping("KeyE", LogicalKey.E, "e", "E", "€")),
            new WindowsLayoutMetadata("kbd-ci-altgr", "Windows CI AltGr Unicode")),
        "iso-physical-layout" => (
            CreateProject(
                "iso-105",
                "Windows CI ISO physical layout",
                CharacterMapping(
                    "IntlBackslash",
                    LogicalKey.InternationalBackslash,
                    "\\",
                    "|"),
                ScanOnlyMapping("Enter", LogicalKey.Enter)),
            new WindowsLayoutMetadata("kbd-ci-iso", "Windows CI ISO physical layout")),
        "special-extended-keys" => (
            CreateProject(
                "ansi-104",
                "Windows CI special and extended keys",
                ScanOnlyMapping("Enter", LogicalKey.Enter),
                ScanOnlyMapping("NumpadEnter", LogicalKey.NumpadEnter),
                ScanOnlyMapping("ArrowLeft", LogicalKey.ArrowLeft),
                ScanOnlyMapping("PrintScreen", LogicalKey.PrintScreen)),
            new WindowsLayoutMetadata("kbd-ci-special", "Windows CI special and extended keys")),
        _ => throw new ArgumentOutOfRangeException(nameof(fixtureName), fixtureName, "Unknown fixture.")
    };

    private static KeyboardProject CreateProject(
        string templateId,
        string name,
        params KeyMapping[] mappings) => new()
    {
        Metadata = new ProjectMetadata
        {
            Name = name,
            Description = "Representative project compiled by Windows integration CI.",
            Version = "1.0.0",
            Language = "en-US"
        },
        Keyboard = new KeyboardTemplateProvider().Load(templateId),
        Layout = new KeyboardLayout { Mappings = [.. mappings] }
    };

    private static KeyMapping CharacterMapping(
        string keyId,
        LogicalKey logicalKey,
        string defaultValue,
        string shiftValue,
        string? altGrValue = null,
        string? shiftAltGrValue = null)
    {
        var mapping = new KeyMapping
        {
            KeyId = keyId,
            LogicalKey = logicalKey,
            Outputs =
            {
                [ModifierLayer.Default] = new CharacterOutput(defaultValue),
                [ModifierLayer.Shift] = new CharacterOutput(shiftValue)
            }
        };
        AddOptionalCharacter(mapping, ModifierLayer.AltGr, altGrValue);
        AddOptionalCharacter(mapping, ModifierLayer.ShiftAltGr, shiftAltGrValue);
        return mapping;
    }

    private static KeyMapping ScanOnlyMapping(string keyId, LogicalKey logicalKey) => new()
    {
        KeyId = keyId,
        LogicalKey = logicalKey
    };

    private static void AddOptionalCharacter(
        KeyMapping mapping,
        ModifierLayer layer,
        string? value)
    {
        if (value is not null)
        {
            mapping.Outputs[layer] = new CharacterOutput(value);
        }
    }
}
