using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

public sealed class LinuxUserVariantViewModel : ObservableObject
{
    private readonly Func<KeyboardProject> _projectProvider;
    private readonly Func<LayoutDerivation?> _derivationProvider;
    private readonly Func<string> _outputDirectoryProvider;
    private readonly Func<(string VariantId, string Description)> _metadataProvider;
    private readonly Action<string, string> _metadataChanged;
    private readonly ILinuxUserVariantWorkflowService _workflow;
    private readonly ILinuxUserVariantInteractionService _interaction;
    private LinuxUserVariantPreparation? _preparation;
    private string _variantId = string.Empty;
    private string _displayName = string.Empty;
    private string _statusText = "Import a system layout to enable user variants.";
    private string _capabilityText = string.Empty;
    private string _pathsText = string.Empty;
    private string _diagnosticsText = string.Empty;
    private string? _generatedBundlePath;
    private bool _isBusy;
    private bool _hasUserEditedMetadata;
    private bool _installedIdentityLocked;
    private int _documentVersion;

    public LinuxUserVariantViewModel(
        Func<KeyboardProject> projectProvider,
        Func<LayoutDerivation?> derivationProvider,
        Func<string> outputDirectoryProvider,
        ILinuxUserVariantWorkflowService workflow,
        ILinuxUserVariantInteractionService? interaction = null,
        Func<(string VariantId, string Description)>? metadataProvider = null,
        Action<string, string>? metadataChanged = null)
    {
        _projectProvider = projectProvider ?? throw new ArgumentNullException(nameof(projectProvider));
        _derivationProvider = derivationProvider ?? throw new ArgumentNullException(nameof(derivationProvider));
        _outputDirectoryProvider = outputDirectoryProvider ?? throw new ArgumentNullException(nameof(outputDirectoryProvider));
        _metadataProvider = metadataProvider ?? (() => (string.Empty, string.Empty));
        _metadataChanged = metadataChanged ?? ((_, _) => { });
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _interaction = interaction ?? new NoOpLinuxUserVariantInteractionService();

        RefreshCommand = new AsyncRelayCommand(
            cancellationToken => RefreshAsync(cancellationToken),
            () => !IsBusy && IsVisible);
        GenerateCommand = new AsyncRelayCommand(GenerateAsync, CanGenerate);
        InstallCommand = new AsyncRelayCommand(InstallAsync, CanInstall);
        UpdateCommand = new AsyncRelayCommand(UpdateAsync, CanUpdate);
        VerifyInstalledCommand = new AsyncRelayCommand(VerifyInstalledAsync, CanVerifyInstalled);
        UninstallCommand = new AsyncRelayCommand(UninstallAsync, CanUninstall);
        OpenOutputCommand = new AsyncRelayCommand(OpenOutputAsync, () =>
            !IsBusy && !string.IsNullOrWhiteSpace(GeneratedBundlePath));
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        ResetForDocument();
    }

    public bool IsVisible => _derivationProvider() is not null;

    public string BaseLayout => _derivationProvider()?.BaseLayoutId ?? "—";

    public string BaseVariant => _derivationProvider()?.BaseVariantId ?? "(default)";

    public string SourceFidelity => _derivationProvider()?.ImportFidelity.ToString() ?? "Unavailable";

    public string VariantId
    {
        get => _variantId;
        set
        {
            if (!CanEditVariantId)
            {
                return;
            }

            if (SetProperty(ref _variantId, value))
            {
                InputsChanged();
            }
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
            {
                InputsChanged();
            }
        }
    }

    public LinuxUserVariantStatus Status =>
        _preparation?.Status ?? LinuxUserVariantStatus.Unavailable;

    public bool CanEditVariantId => !_installedIdentityLocked;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string CapabilityText
    {
        get => _capabilityText;
        private set => SetProperty(ref _capabilityText, value);
    }

    public string PathsText
    {
        get => _pathsText;
        private set => SetProperty(ref _pathsText, value);
    }

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set
        {
            if (SetProperty(ref _diagnosticsText, value))
            {
                OnPropertyChanged(nameof(HasDiagnostics));
            }
        }
    }

    public bool HasDiagnostics => DiagnosticsText.Length > 0;

    public string? GeneratedBundlePath
    {
        get => _generatedBundlePath;
        private set
        {
            if (SetProperty(ref _generatedBundlePath, value))
            {
                OnPropertyChanged(nameof(HasGeneratedBundle));
                OpenOutputCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasGeneratedBundle => !string.IsNullOrWhiteSpace(GeneratedBundlePath);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                NotifyCommandStates();
            }
        }
    }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand GenerateCommand { get; }

    public IAsyncRelayCommand InstallCommand { get; }

    public IAsyncRelayCommand UpdateCommand { get; }

    public IAsyncRelayCommand VerifyInstalledCommand { get; }

    public IAsyncRelayCommand UninstallCommand { get; }

    public IAsyncRelayCommand OpenOutputCommand { get; }

    public IRelayCommand CancelCommand { get; }

    public void ResetForDocument()
    {
        _documentVersion++;
        _hasUserEditedMetadata = false;
        _installedIdentityLocked = false;
        _preparation = null;
        GeneratedBundlePath = null;
        var derivation = _derivationProvider();
        var project = _projectProvider();
        var persisted = _metadataProvider();
        _hasUserEditedMetadata = !string.IsNullOrWhiteSpace(persisted.VariantId) ||
                                 !string.IsNullOrWhiteSpace(persisted.Description);
        _variantId = derivation is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(persisted.VariantId)
                ? KeyboardStudio.Linux.XkbLayoutMetadata.SanitizeIdentifier(
                    $"keyboardstudio_{derivation.BaseVariantId ?? "custom"}",
                    "keyboardstudio_custom")
                : persisted.VariantId;
        _displayName = derivation is null
            ? string.Empty
            : string.IsNullOrWhiteSpace(persisted.Description)
                ? $"{project.Metadata.Name} - KeyboardStudio"
                : persisted.Description;
        OnPropertyChanged(nameof(VariantId));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsVisible));
        OnPropertyChanged(nameof(BaseLayout));
        OnPropertyChanged(nameof(BaseVariant));
        OnPropertyChanged(nameof(SourceFidelity));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanEditVariantId));
        StatusText = derivation is null
            ? "Import a system layout as a new project to enable this workflow."
            : "Checking this host's per-user XKB support…";
        CapabilityText = string.Empty;
        PathsText = string.Empty;
        DiagnosticsText = string.Empty;
        NotifyCommandStates();
    }

    public void NotifyProjectChanged()
    {
        if (!IsVisible)
        {
            return;
        }

        _preparation = null;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanEditVariantId));
        StatusText = "Mappings changed. Refresh or run an action to rebuild the user-variant plan.";
        NotifyCommandStates();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!IsVisible)
        {
            return;
        }

        var version = _documentVersion;
        IsBusy = true;
        StatusText = "Checking layout changes, host capability, and installation ownership…";
        try
        {
            var preparation = await _workflow.InspectAsync(
                _projectProvider(),
                _derivationProvider(),
                _hasUserEditedMetadata ? VariantId : null,
                _hasUserEditedMetadata ? DisplayName : null,
                cancellationToken);
            if (version != _documentVersion)
            {
                return;
            }

            ApplyPreparation(preparation);
        }
        catch (OperationCanceledException)
        {
            StatusText = "User-variant refresh cancelled.";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            _preparation = null;
            StatusText = $"Could not inspect the user variant: {exception.Message}";
            DiagnosticsText = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        var preparation = await PrepareForActionAsync(cancellationToken);
        if (preparation?.CanGenerate != true)
        {
            return;
        }

        await RunOperationAsync(
            () => _workflow.GenerateAsync(
                preparation,
                _outputDirectoryProvider(),
                cancellationToken),
            refreshAfter: false);
    }

    private Task InstallAsync(CancellationToken cancellationToken) =>
        RunLiveOperationAsync("Install", _workflow.InstallOrUpdateAsync, cancellationToken);

    private Task UpdateAsync(CancellationToken cancellationToken) =>
        RunLiveOperationAsync("Update", _workflow.InstallOrUpdateAsync, cancellationToken);

    private Task VerifyInstalledAsync(CancellationToken cancellationToken) =>
        RunLiveOperationAsync("Verify installed", _workflow.VerifyInstalledAsync, cancellationToken);

    private Task UninstallAsync(CancellationToken cancellationToken) =>
        RunLiveOperationAsync("Uninstall", _workflow.UninstallAsync, cancellationToken);

    private async Task RunLiveOperationAsync(
        string action,
        Func<LinuxUserVariantPreparation, CancellationToken, Task<LinuxUserVariantOperationResult>> operation,
        CancellationToken cancellationToken)
    {
        var preparation = await PrepareForActionAsync(cancellationToken);
        if (preparation is not { CanManage: true, Paths: not null })
        {
            return;
        }

        var paths = ExactLivePaths(preparation);
        if (!await _interaction.ConfirmLiveXkbOperationAsync(action, paths))
        {
            StatusText = $"{action} cancelled before any live file was changed.";
            return;
        }

        await RunOperationAsync(() => operation(preparation, cancellationToken), refreshAfter: true);
    }

    private async Task<LinuxUserVariantPreparation?> PrepareForActionAsync(
        CancellationToken cancellationToken)
    {
        if (_preparation is not null)
        {
            return _preparation;
        }

        await RefreshAsync(cancellationToken);
        return _preparation;
    }

    private async Task RunOperationAsync(
        Func<Task<LinuxUserVariantOperationResult>> operation,
        bool refreshAfter)
    {
        IsBusy = true;
        try
        {
            var result = await operation();
            StatusText = result.Message;
            DiagnosticsText = string.Join(
                Environment.NewLine,
                result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            if (result.Success && result.OutputPath is not null)
            {
                GeneratedBundlePath = result.OutputPath;
            }

            if (refreshAfter)
            {
                _preparation = null;
                await RefreshAsync();
                if (result.Success)
                {
                    StatusText = result.Message;
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Linux user-variant operation cancelled; any live changes were rolled back.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OpenOutputAsync()
    {
        if (GeneratedBundlePath is not null)
        {
            await _interaction.OpenDirectoryAsync(GeneratedBundlePath);
        }
    }

    private void Cancel()
    {
        RefreshCommand.Cancel();
        GenerateCommand.Cancel();
        InstallCommand.Cancel();
        UpdateCommand.Cancel();
        VerifyInstalledCommand.Cancel();
        UninstallCommand.Cancel();
    }

    private void ApplyPreparation(LinuxUserVariantPreparation preparation)
    {
        _preparation = preparation;
        _installedIdentityLocked = preparation.IsInstalled;
        if (preparation.Metadata is not null)
        {
            _variantId = preparation.Metadata.PublicVariantId;
            _displayName = preparation.Metadata.Description;
            OnPropertyChanged(nameof(VariantId));
            OnPropertyChanged(nameof(DisplayName));
        }

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(CanEditVariantId));
        StatusText = DescribeStatus(preparation.Status);
        CapabilityText = DescribeCapability(preparation);
        PathsText = preparation.Paths is null
            ? "No safe per-user XKB paths were resolved."
            : $"XKB: {preparation.Paths.UserXkbRoot}{Environment.NewLine}State: {preparation.Paths.KeyboardStudioStateRoot}";
        DiagnosticsText = string.Join(
            Environment.NewLine,
            preparation.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
        NotifyCommandStates();
    }

    private void InputsChanged()
    {
        _hasUserEditedMetadata = true;
        _metadataChanged(VariantId, DisplayName);
        _preparation = null;
        OnPropertyChanged(nameof(Status));
        StatusText = "Variant settings changed. Refresh or run an action to validate them.";
        NotifyCommandStates();
    }

    private bool CanGenerate() => !IsBusy && IsVisible &&
        IsValidVariantId(VariantId) &&
        !string.IsNullOrWhiteSpace(DisplayName) &&
        (_preparation is null || _preparation.CanGenerate);

    private bool CanInstall() => !IsBusy &&
        _preparation is { CanManage: true, Status: LinuxUserVariantStatus.NotInstalled };

    private bool CanUpdate() => !IsBusy &&
        _preparation is { CanManage: true, Status: LinuxUserVariantStatus.UpdateAvailable };

    private bool CanVerifyInstalled() => !IsBusy &&
        _preparation is { CanManage: true, IsInstalled: true };

    private bool CanUninstall() => !IsBusy &&
        _preparation is { CanManage: true, IsInstalled: true };

    private void NotifyCommandStates()
    {
        RefreshCommand.NotifyCanExecuteChanged();
        GenerateCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        UpdateCommand.NotifyCanExecuteChanged();
        VerifyInstalledCommand.NotifyCanExecuteChanged();
        UninstallCommand.NotifyCanExecuteChanged();
        OpenOutputCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    private static IReadOnlyList<string> ExactLivePaths(LinuxUserVariantPreparation preparation) =>
    [
        Path.Combine(preparation.Paths!.UserXkbRoot, "symbols", "keyboardstudio"),
        Path.Combine(preparation.Paths.UserXkbRoot, "symbols", preparation.Metadata!.BaseLayoutId),
        Path.Combine(preparation.Paths.UserXkbRoot, "rules", "evdev.xml"),
        Path.Combine(preparation.Paths.KeyboardStudioStateRoot, "installations.json"),
        Path.Combine(preparation.Paths.KeyboardStudioStateRoot, "journal.json"),
        Path.Combine(preparation.Paths.KeyboardStudioStateRoot, "backups", "<transaction-id>"),
        Path.Combine(preparation.Paths.KeyboardStudioStateRoot, "transactions", "<transaction-id>")
    ];

    private static string DescribeStatus(LinuxUserVariantStatus status) => status switch
    {
        LinuxUserVariantStatus.ExportOnly => "Bundle export is available, but this session cannot use managed per-user installation.",
        LinuxUserVariantStatus.NotInstalled => "Ready to install as a new per-user variant.",
        LinuxUserVariantStatus.Installed => "The per-user variant is installed and matches this project.",
        LinuxUserVariantStatus.UpdateAvailable => "The installed variant differs from the current project or metadata; an update is available.",
        LinuxUserVariantStatus.ExternallyModified => "KeyboardStudio-owned installed content was modified externally; automatic overwrite is blocked.",
        LinuxUserVariantStatus.Broken => "The host-local manifest or one of its installed files is missing or broken.",
        LinuxUserVariantStatus.BaseUnavailable => "The imported system base layout is unavailable on this host; export remains possible.",
        _ => "The project is not ready for a managed Linux user variant."
    };

    private static string DescribeCapability(LinuxUserVariantPreparation preparation)
    {
        var capability = preparation.Capability;
        if (capability is null)
        {
            return "Host capability has not been determined.";
        }

        var version = capability.LibXkbCommonVersion?.ToString() ?? "unknown version";
        var discovery = capability.RegistryDiscovery == KeyboardStudio.Linux.XkbRegistryDiscoverySupport.Available
            ? "desktop registry discovery verified"
            : "desktop registry discovery unavailable";
        return $"{capability.Mode}; {capability.SessionType}; libxkbcommon {version}; {discovery}.";
    }

    private static bool IsValidVariantId(string value) =>
        value.Length is > 0 and <= 64 &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
}
