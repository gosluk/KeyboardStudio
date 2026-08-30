using KeyboardStudio.Build;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using System.Collections.ObjectModel;

namespace KeyboardStudio.App;

public sealed class BuildViewModel : ObservableObject
{
    private readonly Func<KeyboardProject> _projectProvider;
    private readonly ITargetBuildService _buildService;
    private readonly IBuildInteractionService _interactionService;
    private readonly Action? _profileChanged;
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
    private bool _isApplyingProfiles;

    public BuildViewModel(
        Func<KeyboardProject> projectProvider,
        ITargetBuildService buildService,
        IBuildInteractionService? interactionService = null,
        IReadOnlyDictionary<string, ProjectTargetProfile>? targetProfiles = null,
        Action? profileChanged = null,
        IBuildTargetVisibilityPolicy? visibilityPolicy = null)
    {
        _projectProvider = projectProvider ?? throw new ArgumentNullException(nameof(projectProvider));
        _buildService = buildService ?? throw new ArgumentNullException(nameof(buildService));
        _interactionService = interactionService ?? new NoOpBuildInteractionService();
        _profileChanged = profileChanged;
        BuildTargetOptionViewModel[] allTargets =
        [
            new(BuildTarget.WindowsX64, "Windows x64"),
            new(BuildTarget.LinuxXkb, "Linux XKB")
        ];
        var policy = visibilityPolicy ?? new EnvironmentBuildTargetVisibilityPolicy();
        var visibleTargets = Array.FindAll(allTargets, option => policy.IsVisible(option.Target));

        // A policy that hides everything would leave a Build card that cannot build anything.
        // Fall back to the full list rather than presenting dead UI.
        Targets = visibleTargets.Length > 0 ? visibleTargets : allTargets;

        // Profiles are built for every target, visible or not: a hidden target keeps its settings so
        // ExportTargetProfiles still round-trips a document authored on a Windows-enabled build.
        _profiles = CreateProfiles();
        _selectedTarget = Targets[0];
        _profileSettings = _profiles[_selectedTarget.Target];
        _outputDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "KeyboardStudio Builds");
        foreach (var setting in _profiles.Values.SelectMany(settings => settings))
        {
            setting.PropertyChanged += (_, _) => ProfileSettingChanged();
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
        ApplyTargetProfiles(targetProfiles ?? CreateDefaultTargetProfiles());
    }

    public IReadOnlyList<BuildTargetOptionViewModel> Targets { get; }

    /// <summary>
    /// Whether the Build card renders a target selector. With a single visible target there is
    /// nothing to choose, so the view shows the target name as a badge instead.
    /// </summary>
    public bool IsTargetSelectorVisible => Targets.Count > 1;

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
                RefreshReadiness();
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
                SetBuildResult(null);
                RefreshReadiness();
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

    public ObservableCollection<BuildProblemViewModel> Problems { get; } = [];

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
        SetBuildResult(null);
        RefreshReadiness();
    }

    public IReadOnlyDictionary<string, ProjectTargetProfile> ExportTargetProfiles() =>
        new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)
        {
            [BuildProfileTargetIds.WindowsX64] = CreateTargetProfile(
                BuildProfileTargetIds.WindowsX64,
                BuildTarget.WindowsX64),
            [BuildProfileTargetIds.LinuxXkb] = CreateTargetProfile(
                BuildProfileTargetIds.LinuxXkb,
                BuildTarget.LinuxXkb)
        };

    public void ApplyTargetProfiles(IReadOnlyDictionary<string, ProjectTargetProfile> targetProfiles)
    {
        ArgumentNullException.ThrowIfNull(targetProfiles);
        _isApplyingProfiles = true;
        try
        {
            ResetProfiles();
            ApplyTargetProfile(targetProfiles, BuildProfileTargetIds.WindowsX64, BuildTarget.WindowsX64);
            ApplyTargetProfile(targetProfiles, BuildProfileTargetIds.LinuxXkb, BuildTarget.LinuxXkb);
        }
        finally
        {
            _isApplyingProfiles = false;
        }

        SetBuildResult(null);
        RefreshReadiness();
    }

    public static IReadOnlyDictionary<string, ProjectTargetProfile> CreateDefaultTargetProfiles()
    {
        var profiles = CreateProfiles();
        return new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)
        {
            [BuildProfileTargetIds.WindowsX64] = new(
                BuildProfileTargetIds.WindowsX64,
                ToSettings(profiles[BuildTarget.WindowsX64])),
            [BuildProfileTargetIds.LinuxXkb] = new(
                BuildProfileTargetIds.LinuxXkb,
                ToSettings(profiles[BuildTarget.LinuxXkb]))
        };
    }

    public (string VariantId, string Description) GetLinuxUserVariantMetadata()
    {
        var settings = _profiles[BuildTarget.LinuxXkb];
        return (
            settings.Single(setting => setting.Key == BuildProfileKeys.UserVariantId).Value,
            settings.Single(setting => setting.Key == BuildProfileKeys.UserVariantDescription).Value);
    }

    public void SetLinuxUserVariantMetadata(string variantId, string description)
    {
        var settings = _profiles[BuildTarget.LinuxXkb];
        settings.Single(setting => setting.Key == BuildProfileKeys.UserVariantId).Value = variantId;
        settings.Single(setting => setting.Key == BuildProfileKeys.UserVariantDescription).Value = description;
    }

    private void RefreshReadiness()
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
        if (_lastResult is null && !IsBuilding)
        {
            SetProblems(CreateReadinessProblems(_readiness));
        }

        BuildCommand.NotifyCanExecuteChanged();
    }

    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        if (!CanStartBuild())
        {
            return;
        }

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
            var isUnverified = Problems.Any(problem =>
                problem.Kind == BuildProblemKind.OptionalVerifierUnavailable);
            Status = result.Success
                ? isUnverified
                    ? "Build completed without external XKB verification."
                    : "Build completed successfully."
                : Problems.Count > 0
                    ? $"Build failed: {Problems[0].Category}."
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
            SetProblems([
                new BuildProblemViewModel(
                    BuildProblemKind.SourceGeneration,
                    "Source generation error",
                    BuildDiagnosticSeverity.Error,
                    "GEN_SOURCE",
                    exception.Message)
            ]);
        }
        finally
        {
            IsBuilding = false;
            RefreshReadiness();
        }
    }

    private void CancelBuild() => BuildCommand.Cancel();

    private void ProfileSettingChanged()
    {
        SetBuildResult(null);
        RefreshReadiness();
        if (!_isApplyingProfiles)
        {
            _profileChanged?.Invoke();
        }
    }

    private ProjectTargetProfile CreateTargetProfile(string targetId, BuildTarget target) =>
        new(targetId, ToSettings(_profiles[target]));

    private void ResetProfiles()
    {
        var defaults = CreateProfiles();
        foreach (var target in _profiles.Keys)
        {
            foreach (var setting in _profiles[target])
            {
                setting.Value = defaults[target].Single(candidate =>
                    candidate.Key == setting.Key).Value;
            }
        }
    }

    private void ApplyTargetProfile(
        IReadOnlyDictionary<string, ProjectTargetProfile> profiles,
        string targetId,
        BuildTarget target)
    {
        if (!profiles.TryGetValue(targetId, out var profile) ||
            !string.Equals(profile.Target, targetId, StringComparison.Ordinal))
        {
            return;
        }

        foreach (var setting in _profiles[target])
        {
            if (profile.Settings.TryGetValue(setting.Key, out var value))
            {
                setting.Value = value;
            }
        }
    }

    private static Dictionary<string, string> ToSettings(
        IReadOnlyList<BuildProfileSettingViewModel> settings) =>
        settings.Where(setting => setting.IsVisible || !string.IsNullOrEmpty(setting.Value)).ToDictionary(
            setting => setting.Key,
            setting => setting.Value,
            StringComparer.Ordinal);

    private async Task OpenOutputDirectoryAsync()
    {
        await RunActionAsync(
            () => _interactionService.OpenDirectoryAsync(OutputDirectory),
            "Opened output directory.");
    }

    private async Task InspectGeneratedFileAsync()
    {
        if (SelectedGeneratedFile is null)
        {
            return;
        }

        var generatedFile = SelectedGeneratedFile;
        await RunActionAsync(
            () => _interactionService.ShowGeneratedTextAsync(
                generatedFile.Name,
                generatedFile.Content),
            $"Opened {generatedFile.Name}.");
    }

    private async Task CopyBuildLogAsync()
    {
        await RunActionAsync(
            () => _interactionService.CopyTextAsync(BuildLog),
            "Copied build log.");
    }

    private async Task CopyArtifactPathAsync()
    {
        var artifactPath = ArtifactPath;
        if (artifactPath is null)
        {
            return;
        }

        await RunActionAsync(
            () => _interactionService.CopyTextAsync(artifactPath),
            "Copied artifact path.");
    }

    private async Task RunActionAsync(Func<Task> action, string successStatus)
    {
        try
        {
            await action();
            ActionStatus = successStatus;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            ActionStatus = $"Action failed: {exception.Message}";
        }
    }

    private void SetBuildResult(KeyboardBuildResult? result)
    {
        _lastResult = result;
        ArtifactPath = result?.Artifact?.ArtifactPath;
        GeneratedFiles = result?.Artifact?.GeneratedFiles ?? [];
        SelectedGeneratedFile = GeneratedFiles.Count > 0 ? GeneratedFiles[0] : null;
        BuildLog = CreateBuildLog(result);
        ActionStatus = string.Empty;
        SetProblems(CreateResultProblems(result));
        OnPropertyChanged(nameof(BuildLog));
        OpenOutputDirectoryCommand.NotifyCanExecuteChanged();
        CopyBuildLogCommand.NotifyCanExecuteChanged();
    }

    private void SetProblems(IEnumerable<BuildProblemViewModel> problems)
    {
        Problems.Clear();
        foreach (var problem in problems)
        {
            Problems.Add(problem);
        }
    }

    private static IEnumerable<BuildProblemViewModel> CreateReadinessProblems(BuildReadiness readiness)
    {
        foreach (var issue in readiness.CommonIssues)
        {
            yield return CreateProblem(
                BuildProblemKind.ProjectValidation,
                issue.Severity,
                issue.Code,
                issue.Message);
        }

        foreach (var issue in readiness.TargetIssues)
        {
            yield return CreateProblem(
                BuildProblemKind.TargetCompatibility,
                issue.Severity,
                issue.Code,
                issue.Message);
        }

        foreach (var diagnostic in readiness.Environment.Diagnostics)
        {
            yield return CreateProblem(
                diagnostic.Code == "KSL004"
                    ? BuildProblemKind.OptionalVerifierUnavailable
                    : BuildProblemKind.MissingRequiredToolchain,
                readiness.Environment.Available
                    ? ValidationSeverity.Warning
                    : ValidationSeverity.Error,
                diagnostic.Code,
                diagnostic.Message);
        }

        if (!readiness.Environment.Available && readiness.Environment.Diagnostics.Count == 0)
        {
            yield return CreateProblem(
                BuildProblemKind.MissingRequiredToolchain,
                ValidationSeverity.Error,
                "ENV001",
                readiness.Environment.Message);
        }
    }

    private static IEnumerable<BuildProblemViewModel> CreateResultProblems(KeyboardBuildResult? result)
    {
        if (result is null)
        {
            yield break;
        }

        foreach (var issue in result.ValidationIssues)
        {
            var kind = issue.Code.StartsWith("KSW", StringComparison.Ordinal) ||
                       issue.Code is "KSL001" or "KSL002" or "TARGET_OUTPUT" or "TARGET_PROFILE"
                ? BuildProblemKind.TargetCompatibility
                : BuildProblemKind.ProjectValidation;
            yield return CreateProblem(kind, issue.Severity, issue.Code, issue.Message);
        }

        foreach (var diagnostic in result.Artifact?.Diagnostics ?? [])
        {
            yield return CreateProblem(
                ClassifyArtifactDiagnostic(diagnostic.Code),
                diagnostic.Severity,
                diagnostic.Code,
                diagnostic.Message);
        }
    }

    private static BuildProblemKind ClassifyArtifactDiagnostic(string code) =>
        code switch
        {
            "KSL001" or "KSL002" => BuildProblemKind.TargetCompatibility,
            "GEN_SOURCE" or "KSL006" => BuildProblemKind.SourceGeneration,
            "KSL004" => BuildProblemKind.OptionalVerifierUnavailable,
            "KSL003" or "KSL005" => BuildProblemKind.ArtifactVerification,
            _ when code.StartsWith("PE_", StringComparison.Ordinal) => BuildProblemKind.ArtifactVerification,
            _ when code.StartsWith("ENV", StringComparison.Ordinal) => BuildProblemKind.MissingRequiredToolchain,
            _ => BuildProblemKind.CompilerOrLinker
        };

    private static BuildProblemViewModel CreateProblem(
        BuildProblemKind kind,
        ValidationSeverity severity,
        string code,
        string message) =>
        new(
            kind,
            GetCategory(kind),
            severity switch
            {
                ValidationSeverity.Info => BuildDiagnosticSeverity.Info,
                ValidationSeverity.Warning => BuildDiagnosticSeverity.Warning,
                _ => BuildDiagnosticSeverity.Error
            },
            code,
            message);

    private static BuildProblemViewModel CreateProblem(
        BuildProblemKind kind,
        BuildDiagnosticSeverity severity,
        string code,
        string message) =>
        new(kind, GetCategory(kind), severity, code, message);

    private static string GetCategory(BuildProblemKind kind) =>
        kind switch
        {
            BuildProblemKind.ProjectValidation => "Project validation error",
            BuildProblemKind.TargetCompatibility => "Target compatibility error",
            BuildProblemKind.SourceGeneration => "Source generation error",
            BuildProblemKind.MissingRequiredToolchain => "Missing required toolchain",
            BuildProblemKind.OptionalVerifierUnavailable => "Optional verifier unavailable",
            BuildProblemKind.CompilerOrLinker => "Compiler or linker error",
            BuildProblemKind.ArtifactVerification => "Artifact verification error",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown build problem kind.")
        };

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
            [BuildTarget.LinuxXkb] =
            [
                new(BuildProfileKeys.LayoutId, "Layout ID", "keyboardstudio"),
                new(BuildProfileKeys.SectionId, "Section ID", "basic"),
                new(BuildProfileKeys.Description, "Description", "KeyboardStudio layout"),
                new(BuildProfileKeys.UserVariantId, "User variant ID", string.Empty, isVisible: false),
                new(BuildProfileKeys.UserVariantDescription, "User variant name", string.Empty, isVisible: false)
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
