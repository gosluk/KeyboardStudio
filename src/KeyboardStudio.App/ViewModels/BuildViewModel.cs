using KeyboardStudio.Build;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;
using System.Collections.ObjectModel;

namespace KeyboardStudio.App;

public sealed class BuildViewModel : ObservableObject
{
    private readonly Func<KeyboardProject> _projectProvider;
    private readonly ITargetBuildService _buildService;
    private readonly IBuildInteractionService _interactionService;
    private readonly Dictionary<BuildTarget, IReadOnlyList<BuildProfileSettingViewModel>> _profiles;
    private BuildTargetOptionViewModel _selectedTarget;
    private IReadOnlyList<BuildProfileSettingViewModel> _profileSettings;
    private string _outputDirectory;
    private string _environmentStatus = string.Empty;
    private string _validationStatus = string.Empty;
    private string _status = "Ready to build.";
    private string? _artifactPath;
    private BuildReadiness? _readiness;
    private bool _isBuilding;
    private KeyboardBuildResult? _lastResult;
    private IReadOnlyList<BuildTextFile> _generatedFiles = [];
    private BuildTextFile? _selectedGeneratedFile;
    private string _actionStatus = string.Empty;

    public BuildViewModel(
        Func<KeyboardProject> projectProvider,
        ITargetBuildService buildService,
        IBuildInteractionService? interactionService = null)
    {
        _projectProvider = projectProvider ?? throw new ArgumentNullException(nameof(projectProvider));
        _buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        _interactionService = interactionService ?? new NoOpBuildInteractionService();
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
        foreach (var setting in _profiles.Values.SelectMany(settings => settings))
        {
            setting.PropertyChanged += (_, _) => Refresh();
        }

        BuildCommand = new AsyncRelayCommand(BuildAsync, CanStartBuild);
        CancelBuildCommand = new RelayCommand(CancelBuild, () => IsBuilding);
        OpenOutputDirectoryCommand = new AsyncRelayCommand(
            OpenOutputDirectoryAsync,
            () => _lastResult is not null);
        InspectGeneratedFileCommand = new AsyncRelayCommand(
            InspectGeneratedFileAsync,
            () => SelectedGeneratedFile is not null);
        CopyBuildLogCommand = new AsyncRelayCommand(
            CopyBuildLogAsync,
            () => !string.IsNullOrWhiteSpace(BuildLog));
        CopyArtifactPathCommand = new AsyncRelayCommand(
            CopyArtifactPathAsync,
            () => HasArtifact);
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
                SetBuildResult(null);
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
        set
        {
            if (SetProperty(ref _outputDirectory, value))
            {
                Refresh();
            }
        }
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
                CopyArtifactPathCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasArtifact => !string.IsNullOrWhiteSpace(ArtifactPath);

    public IAsyncRelayCommand BuildCommand { get; }

    public IRelayCommand CancelBuildCommand { get; }

    public IAsyncRelayCommand OpenOutputDirectoryCommand { get; }

    public IAsyncRelayCommand InspectGeneratedFileCommand { get; }

    public IAsyncRelayCommand CopyBuildLogCommand { get; }

    public IAsyncRelayCommand CopyArtifactPathCommand { get; }

    public ObservableCollection<BuildStageViewModel> Stages { get; } = [];

    public IReadOnlyList<BuildTextFile> GeneratedFiles
    {
        get => _generatedFiles;
        private set => SetProperty(ref _generatedFiles, value);
    }

    public BuildTextFile? SelectedGeneratedFile
    {
        get => _selectedGeneratedFile;
        set
        {
            if (SetProperty(ref _selectedGeneratedFile, value))
            {
                InspectGeneratedFileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string BuildLog { get; private set; } = string.Empty;

    public string ActionStatus
    {
        get => _actionStatus;
        private set => SetProperty(ref _actionStatus, value);
    }

    public bool IsBuilding
    {
        get => _isBuilding;
        private set
        {
            if (SetProperty(ref _isBuilding, value))
            {
                BuildCommand.NotifyCanExecuteChanged();
                CancelBuildCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public void Refresh()
    {
        var settings = GetProfileSettings();
        _readiness = _buildService.GetReadiness(
            _projectProvider(),
            SelectedTarget.Target,
            settings,
            OutputDirectory);
        EnvironmentStatus = _readiness.Environment.Message;
        var commonErrorCount = _readiness.CommonIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
        var targetErrorCount = _readiness.TargetIssues.Count(issue => issue.Severity == ValidationSeverity.Error);
        ValidationStatus = commonErrorCount + targetErrorCount > 0
            ? $"{commonErrorCount} common and {targetErrorCount} target error(s) block this build."
            : "Common and selected-target validation passed.";
        BuildCommand.NotifyCanExecuteChanged();
    }

    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        IsBuilding = true;
        SetBuildResult(null);
        Stages.Clear();
        Status = $"Building {SelectedTarget.DisplayName}…";
        try
        {
            var options = new BuildOptions(SelectedTarget.Target, OutputDirectory);
            var settings = GetProfileSettings();
            var progress = new DirectProgress<BuildStageProgress>(UpdateStage);
            var result = await _buildService.BuildAsync(
                _projectProvider(),
                options,
                settings,
                progress,
                cancellationToken);
            SetBuildResult(result);
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
        finally
        {
            IsBuilding = false;
            Refresh();
        }
    }

    private void CancelBuild() => BuildCommand.Cancel();

    private async Task OpenOutputDirectoryAsync()
    {
        await _interactionService.OpenDirectoryAsync(OutputDirectory);
        ActionStatus = "Opened output directory.";
    }

    private async Task InspectGeneratedFileAsync()
    {
        if (SelectedGeneratedFile is null)
        {
            return;
        }

        await _interactionService.ShowGeneratedTextAsync(
            SelectedGeneratedFile.Name,
            SelectedGeneratedFile.Content);
        ActionStatus = $"Opened {SelectedGeneratedFile.Name}.";
    }

    private async Task CopyBuildLogAsync()
    {
        await _interactionService.CopyTextAsync(BuildLog);
        ActionStatus = "Copied build log.";
    }

    private async Task CopyArtifactPathAsync()
    {
        if (ArtifactPath is null)
        {
            return;
        }

        await _interactionService.CopyTextAsync(ArtifactPath);
        ActionStatus = "Copied artifact path.";
    }

    private void SetBuildResult(KeyboardBuildResult? result)
    {
        _lastResult = result;
        ArtifactPath = result?.Artifact?.ArtifactPath;
        GeneratedFiles = result?.Artifact?.GeneratedFiles ?? [];
        SelectedGeneratedFile = GeneratedFiles.Count > 0 ? GeneratedFiles[0] : null;
        BuildLog = CreateBuildLog(result);
        ActionStatus = string.Empty;
        OnPropertyChanged(nameof(BuildLog));
        OpenOutputDirectoryCommand.NotifyCanExecuteChanged();
        CopyBuildLogCommand.NotifyCanExecuteChanged();
    }

    private static string CreateBuildLog(KeyboardBuildResult? result)
    {
        if (result is null)
        {
            return string.Empty;
        }

        var lines = result.ValidationIssues.Select(issue =>
            $"[{issue.Severity}] {issue.Code}: {issue.Message}").Concat(
            result.Artifact?.Diagnostics.Select(diagnostic =>
                $"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}") ?? []);
        var diagnostics = string.Join(Environment.NewLine, lines);
        var rawLog = result.Artifact?.RawLog ?? string.Empty;
        return string.Join(
            Environment.NewLine,
            new[] { diagnostics, rawLog }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private void UpdateStage(BuildStageProgress progress)
    {
        var stage = Stages.FirstOrDefault(candidate => candidate.Name == progress.Name);
        if (stage is null)
        {
            Stages.Add(new BuildStageViewModel(progress.Name, progress.State));
        }
        else
        {
            stage.Update(progress.State);
        }
    }

    private bool CanStartBuild() => !IsBuilding && _readiness?.CanBuild == true;

    private Dictionary<string, string> GetProfileSettings() =>
        ProfileSettings.ToDictionary(
            setting => setting.Key,
            setting => setting.Value,
            StringComparer.Ordinal);

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

    private sealed class DirectProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public DirectProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
    }
}
