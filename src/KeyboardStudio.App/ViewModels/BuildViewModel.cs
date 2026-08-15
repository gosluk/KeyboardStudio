using KeyboardStudio.Build;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class BuildViewModel : ObservableObject
{
    private readonly Func<KeyboardProject> _projectProvider;
    private readonly ITargetBuildService _buildService;
    private readonly IKeyboardProjectValidator _validator;
    private readonly Dictionary<BuildTarget, IReadOnlyList<BuildProfileSettingViewModel>> _profiles;
    private BuildTargetOptionViewModel _selectedTarget;
    private IReadOnlyList<BuildProfileSettingViewModel> _profileSettings;
    private string _outputDirectory;
    private string _environmentStatus = string.Empty;
    private string _validationStatus = string.Empty;
    private string _status = "Ready to build.";
    private string? _artifactPath;

    public BuildViewModel(
        Func<KeyboardProject> projectProvider,
        ITargetBuildService buildService,
        IKeyboardProjectValidator validator)
    {
        _projectProvider = projectProvider ?? throw new ArgumentNullException(nameof(projectProvider));
        _buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        Targets =
        [
            new(BuildTarget.WindowsX64, "Windows x64"),
            new(BuildTarget.WindowsArm64, "Windows ARM64"),
            new(BuildTarget.LinuxXkb, "Linux XKB")
        ];
        _profiles = CreateProfiles();
        _selectedTarget = Targets[0];
        _profileSettings = _profiles[_selectedTarget.Target];
        _outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "KeyboardStudio Builds");
        BuildCommand = new AsyncRelayCommand(BuildAsync);
        Refresh();
    }

    public IReadOnlyList<BuildTargetOptionViewModel> Targets { get; }

    public BuildTargetOptionViewModel SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedTarget, value))
            {
                ProfileSettings = _profiles[value.Target];
                ArtifactPath = null;
                Refresh();
            }
        }
    }

    public IReadOnlyList<BuildProfileSettingViewModel> ProfileSettings
    {
        get => _profileSettings;
        private set => SetProperty(ref _profileSettings, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => SetProperty(ref _outputDirectory, value);
    }

    public string EnvironmentStatus
    {
        get => _environmentStatus;
        private set => SetProperty(ref _environmentStatus, value);
    }

    public string ValidationStatus
    {
        get => _validationStatus;
        private set => SetProperty(ref _validationStatus, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string? ArtifactPath
    {
        get => _artifactPath;
        private set
        {
            if (SetProperty(ref _artifactPath, value))
            {
                OnPropertyChanged(nameof(HasArtifact));
            }
        }
    }

    public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactPath);

    public IAsyncRelayCommand BuildCommand { get; }

    public void Refresh()
    {
        var environment = _buildService.GetEnvironmentStatus(SelectedTarget.Target);
        EnvironmentStatus = environment.Message;
        var validation = _validator.Validate(_projectProvider());
        ValidationStatus = validation.HasErrors
            ? $"{validation.Issues.Count(issue => issue.Severity == ValidationSeverity.Error)} blocking validation error(s)."
            : "Project validation passed.";
    }

    private async Task BuildAsync()
    {
        ArtifactPath = null;
        Status = $"Building {SelectedTarget.DisplayName}…";
        try
        {
            var options = new BuildOptions(SelectedTarget.Target, OutputDirectory);
            var settings = ProfileSettings.ToDictionary(
                setting => setting.Key,
                setting => setting.Value,
                StringComparer.Ordinal);
            var result = await _buildService.BuildAsync(_projectProvider(), options, settings);
            ArtifactPath = result.Artifact?.ArtifactPath;
            Status = result.Success
                ? "Build completed successfully."
                : "Build failed. Review the build diagnostics.";
        }
        catch (OperationCanceledException)
        {
            Status = "Build cancelled.";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Status = $"Build failed: {exception.Message}";
        }
    }

    private static Dictionary<BuildTarget, IReadOnlyList<BuildProfileSettingViewModel>> CreateProfiles() =>
        new Dictionary<BuildTarget, IReadOnlyList<BuildProfileSettingViewModel>>
        {
            [BuildTarget.WindowsX64] = CreateWindowsProfile(),
            [BuildTarget.WindowsArm64] = CreateWindowsProfile(),
            [BuildTarget.LinuxXkb] =
            [
                new(BuildProfileKeys.LayoutId, "Layout ID", "keyboardstudio"),
                new(BuildProfileKeys.SectionId, "Section ID", "basic"),
                new(BuildProfileKeys.Description, "Description", "KeyboardStudio layout")
            ]
        };

    private static IReadOnlyList<BuildProfileSettingViewModel> CreateWindowsProfile() =>
    [
        new(BuildProfileKeys.LayoutId, "Layout ID", "keyboardstudio"),
        new(BuildProfileKeys.LayoutName, "Layout name", "KeyboardStudio layout"),
        new(BuildProfileKeys.FileVersion, "File version", "1.0.0.0"),
        new(BuildProfileKeys.CompanyName, "Company", "KeyboardStudio")
    ];
}
