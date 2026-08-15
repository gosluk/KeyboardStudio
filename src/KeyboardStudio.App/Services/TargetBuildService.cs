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

    public Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        IReadOnlyDictionary<string, string> profileSettings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(profileSettings);

        var backend = CreateBackend(options.Target, profileSettings);
        var orchestrator = new BuildOrchestrator(
            _commonValidator,
            new BuildBackendResolver([backend]));
        return orchestrator.BuildAsync(project, options, cancellationToken);
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
