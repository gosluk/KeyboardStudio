using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using KeyboardStudio.Windows;

namespace KeyboardStudio.App;

public sealed class TargetBuildService : ITargetBuildService
{
    private readonly IKeyboardProjectValidator _commonValidator;
    private readonly IBuildEnvironment _windowsEnvironment;

    public TargetBuildService()
        : this(new KeyboardProjectValidator(), new WindowsBuildEnvironment())
    {
    }

    public TargetBuildService(
        IKeyboardProjectValidator commonValidator,
        IBuildEnvironment windowsEnvironment)
    {
        _commonValidator = commonValidator ?? throw new ArgumentNullException(nameof(commonValidator));
        _windowsEnvironment = windowsEnvironment ?? throw new ArgumentNullException(nameof(windowsEnvironment));
    }

    public BuildEnvironmentStatus GetEnvironmentStatus(BuildTarget target) =>
        target == BuildTarget.LinuxXkb
            ? new LinuxXkbBuildBackend(CreateXkbMetadata(
                new Dictionary<string, string>(StringComparer.Ordinal))).GetStatus(target)
            : _windowsEnvironment.GetStatus(target);

    public BuildReadiness GetReadiness(
        KeyboardProject project,
        BuildTarget target,
        IReadOnlyDictionary<string, string> profileSettings,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(profileSettings);

        var common = _commonValidator.Validate(project).Issues;
        var targetIssues = ValidateTarget(project, target, profileSettings, outputDirectory);
        return new BuildReadiness(
            GetEnvironmentStatus(target),
            common,
            targetIssues);
    }

    public async Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        IReadOnlyDictionary<string, string> profileSettings,
        IProgress<BuildStageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(profileSettings);

        var readiness = GetReadiness(
            project,
            options.Target,
            profileSettings,
            options.OutputDirectory);
        if (!readiness.CanBuild)
        {
            var validationIssues = readiness.CommonIssues.Concat(readiness.TargetIssues).ToArray();
            var environmentDiagnostics = readiness.Environment.Available
                ? []
                : readiness.Environment.Diagnostics.Select(diagnostic => new BuildArtifactDiagnostic(
                    BuildDiagnosticSeverity.Error,
                    diagnostic.Code,
                    diagnostic.Message)).ToArray();
            return new KeyboardBuildResult(
                false,
                validationIssues,
                environmentDiagnostics.Length == 0
                    ? null
                    : new ArtifactBuildResult(false, null, environmentDiagnostics));
        }

        var backend = CreateBackend(options.Target, profileSettings);
        var orchestrator = new BuildOrchestrator(
            _commonValidator,
            new BuildBackendResolver([backend]));
        try
        {
            return await orchestrator.BuildAsync(project, options, progress, cancellationToken);
        }
        catch (Exception exception) when (
            exception is WindowsTranslationException or ArgumentException or InvalidOperationException)
        {
            progress?.Report(new BuildStageProgress(
                options.Target == BuildTarget.LinuxXkb
                    ? BuildStageNames.GeneratingXkb
                    : BuildStageNames.Generating,
                BuildStageState.Failed));
            progress?.Report(new BuildStageProgress(BuildStageNames.Failed, BuildStageState.Failed));
            return new KeyboardBuildResult(
                false,
                [],
                new ArtifactBuildResult(
                    false,
                    null,
                    [new BuildArtifactDiagnostic(
                        BuildDiagnosticSeverity.Error,
                        "GEN_SOURCE",
                        $"Source generation failed: {exception.Message}")]));
        }
    }

    private static List<ValidationIssue> ValidateTarget(
        KeyboardProject project,
        BuildTarget target,
        IReadOnlyDictionary<string, string> settings,
        string outputDirectory)
    {
        var issues = new List<ValidationIssue>();
        if (!TryValidateOutputDirectory(outputDirectory, out var outputError))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "TARGET_OUTPUT",
                outputError));
        }

        if (target is BuildTarget.WindowsX64 or BuildTarget.WindowsArm64)
        {
            issues.AddRange(new WindowsCompatibilityValidationRule().Validate(project));
            var version = GetSetting(settings, BuildProfileKeys.FileVersion, "1.0.0.0");
            if (!Version.TryParse(version, out var parsedVersion) ||
                parsedVersion.Major > ushort.MaxValue ||
                parsedVersion.Minor > ushort.MaxValue ||
                parsedVersion.Build > ushort.MaxValue ||
                parsedVersion.Revision > ushort.MaxValue)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "TARGET_PROFILE",
                    "Windows file version must contain two to four numeric parts between 0 and 65535."));
            }
        }
        else if (target == BuildTarget.LinuxXkb)
        {
            var translation = new XkbLayoutTranslator().Translate(project, CreateXkbMetadata(settings));
            issues.AddRange(translation.Diagnostics.Select(diagnostic => new ValidationIssue(
                ValidationSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.KeyId)));
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported build target.");
        }

        return issues;
    }

    private static bool TryValidateOutputDirectory(string path, out string error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "An output directory is required.";
            return false;
        }

        try
        {
            _ = Path.GetFullPath(path);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"The output directory is invalid: {exception.Message}";
            return false;
        }
    }

    private IBuildBackend CreateBackend(
        BuildTarget target,
        IReadOnlyDictionary<string, string> settings) =>
        target switch
        {
            BuildTarget.WindowsX64 or BuildTarget.WindowsArm64 => CreateWindowsBackend(settings),
            BuildTarget.LinuxXkb => new LinuxXkbBuildBackend(CreateXkbMetadata(settings)),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported build target.")
        };

    private WindowsBuildBackend CreateWindowsBackend(IReadOnlyDictionary<string, string> settings)
    {
        var metadata = new WindowsLayoutMetadata(
            GetSetting(settings, BuildProfileKeys.LayoutId, "keyboardstudio"),
            GetSetting(settings, BuildProfileKeys.LayoutName, "KeyboardStudio layout"),
            GetSetting(settings, BuildProfileKeys.FileVersion, "1.0.0.0"),
            GetSetting(settings, BuildProfileKeys.CompanyName, "KeyboardStudio"));
        return new WindowsBuildBackend(
            new WindowsArtifactGenerator(metadata),
            _windowsEnvironment,
            new MsvcKeyboardCompiler(_windowsEnvironment, new ProcessRunner()));
    }

    private static XkbLayoutMetadata CreateXkbMetadata(IReadOnlyDictionary<string, string> settings) =>
        new(
            GetSetting(settings, BuildProfileKeys.LayoutId, "keyboardstudio"),
            GetSetting(settings, BuildProfileKeys.SectionId, "basic"),
            GetSetting(settings, BuildProfileKeys.Description, "KeyboardStudio layout"));

    private static string GetSetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string fallback) =>
        settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;
}
