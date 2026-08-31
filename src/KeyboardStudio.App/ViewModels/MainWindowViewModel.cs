using KeyboardStudio.Build;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using KeyboardStudio.Linux;
using KeyboardStudio.Windows;

namespace KeyboardStudio.App;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string DefaultTemplateId = "iso-105";

    private readonly ProjectDocumentService _documentService;
    private readonly IProjectInteractionService _interactionService;
    private readonly ILayoutImportCatalog _importCatalog;
    private readonly IHostLayoutProbe _hostLayoutProbe;
    private readonly IKeyboardTemplateProvider _templateProvider;
    private readonly IKeyboardProjectValidator _validator;
    private readonly ISeedProjectSource _seedProjectSource;
    private DiagnosticsViewModel _diagnostics;
    private ValidationIssue? _hostImportIssue;
    private KeyboardProject _startupProject;
    private string _importStatus = string.Empty;
    private KeyboardTemplateDescriptor _selectedTemplate;
    private KeyboardProject _project;
    private KeyboardEditorViewModel _editor;

    public MainWindowViewModel()
        : this(new KeyboardTemplateProvider(), new NoOpProjectInteractionService())
    {
    }

    public MainWindowViewModel(IKeyboardTemplateProvider templateProvider)
        : this(templateProvider, new NoOpProjectInteractionService())
    {
    }

    public MainWindowViewModel(IProjectInteractionService interactionService)
        : this(new KeyboardTemplateProvider(), interactionService)
    {
    }

    public MainWindowViewModel(
        IProjectInteractionService interactionService,
        AppearanceViewModel appearance)
        : this(
            new KeyboardTemplateProvider(),
            interactionService,
            CreateDefaultValidator(),
            new EmbeddedSeedProjectSource(),
            new EnvironmentBuildTargetVisibilityPolicy(),
            appearance: appearance)
    {
    }

    public MainWindowViewModel(
        IKeyboardTemplateProvider templateProvider,
        IProjectInteractionService interactionService)
        : this(templateProvider, interactionService, CreateDefaultValidator())
    {
    }

    public MainWindowViewModel(
        IKeyboardTemplateProvider templateProvider,
        IProjectInteractionService interactionService,
        IKeyboardProjectValidator validator)
        : this(templateProvider, interactionService, validator, new EmbeddedSeedProjectSource())
    {
    }

    public MainWindowViewModel(
        IKeyboardTemplateProvider templateProvider,
        IProjectInteractionService interactionService,
        IKeyboardProjectValidator validator,
        ISeedProjectSource seedProjectSource)
        : this(
            templateProvider,
            interactionService,
            validator,
            seedProjectSource,
            new EnvironmentBuildTargetVisibilityPolicy())
    {
    }

    public MainWindowViewModel(
        IKeyboardTemplateProvider templateProvider,
        IProjectInteractionService interactionService,
        IKeyboardProjectValidator validator,
        ISeedProjectSource seedProjectSource,
        IBuildTargetVisibilityPolicy buildTargetVisibility,
        ILayoutImportCatalog? importCatalog = null,
        IHostLayoutProbe? hostLayoutProbe = null,
        ILinuxUserVariantWorkflowService? linuxUserVariantWorkflow = null,
        AppearanceViewModel? appearance = null)
    {
        ArgumentNullException.ThrowIfNull(templateProvider);
        ArgumentNullException.ThrowIfNull(interactionService);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(seedProjectSource);
        ArgumentNullException.ThrowIfNull(buildTargetVisibility);

        _templateProvider = templateProvider;
        _importCatalog = importCatalog ?? HostLayoutImportCatalog.Create(templateProvider);
        _hostLayoutProbe = hostLayoutProbe ?? HostLayoutImportCatalog.CreateHostProbe();
        _interactionService = interactionService;
        _validator = validator;
        _seedProjectSource = seedProjectSource;
        Templates = templateProvider.Templates;
        _selectedTemplate = Templates.FirstOrDefault(template => template.Id == DefaultTemplateId)
            ?? Templates[0];
        _documentService = new ProjectDocumentService(
            new JsonKeyboardProjectDocumentStore(),
            () => new KeyboardProjectDocument(
                CreateProject(_selectedTemplate),
                BuildViewModel.CreateDefaultTargetProfiles()));
        _project = _documentService.CreateNew();
        _startupProject = _project;
        _editor = CreateEditor(_project, _selectedTemplate);
        _diagnostics = CreateDiagnostics(_editor);
        RefreshDiagnostics();
        Build = new BuildViewModel(
            () => Project,
            new TargetBuildService(),
            interactionService as IBuildInteractionService,
            _documentService.CurrentTargetProfiles,
            BuildProfileChanged,
            buildTargetVisibility);
        LinuxVariant = new LinuxUserVariantViewModel(
            () => Project,
            () => _documentService.CurrentLayoutDerivation,
            () => Build.OutputDirectory,
            linuxUserVariantWorkflow ?? new LinuxUserVariantWorkflowService(),
            interactionService as ILinuxUserVariantInteractionService,
            Build.GetLinuxUserVariantMetadata,
            Build.SetLinuxUserVariantMetadata);
        NewCommand = new AsyncRelayCommand(NewDocumentAsync);
        OpenCommand = new AsyncRelayCommand(OpenDocumentAsync);
        SaveCommand = new AsyncRelayCommand(SaveDocumentAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsDocumentAsync);
        ImportLayoutCommand = new AsyncRelayCommand(ImportLayoutAsync, () => CanImportLayout);
        ImportFromFileCommand = new AsyncRelayCommand(ImportFromFileAsync, () => CanImportLayout);

        // Appearance is application state, not document state. It is reachable from this view model
        // because the header presents it, and it holds no reference back to the document.
        Appearance = appearance ?? new AppearanceViewModel();
    }

    public IReadOnlyList<KeyboardTemplateDescriptor> Templates { get; }

    public KeyboardTemplateDescriptor SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SetProperty(ref _selectedTemplate, value);
        }
    }

    public KeyboardProject Project
    {
        get => _project;
        private set => SetProperty(ref _project, value);
    }

    public KeyboardEditorViewModel Editor
    {
        get => _editor;
        private set => SetProperty(ref _editor, value);
    }

    public AppearanceViewModel Appearance { get; }
    public BuildViewModel Build { get; }
    public LinuxUserVariantViewModel LinuxVariant { get; }
    public IAsyncRelayCommand NewCommand { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand SaveAsCommand { get; }
    public IAsyncRelayCommand ImportLayoutCommand { get; }
    public IAsyncRelayCommand ImportFromFileCommand { get; }

    /// <summary>
    /// Whether any source can list layouts on this host. A host with no installed keyboard
    /// database gets the action disabled rather than a dialog that opens onto nothing.
    /// </summary>
    public bool CanImportLayout => _importCatalog.HasAvailableSources;

    /// <summary>
    /// What the last import did, or empty when none has run. It sits beside the document status
    /// rather than in the build card because it describes the document, not a build.
    /// </summary>
    public string ImportStatus
    {
        get => _importStatus;
        private set
        {
            if (SetProperty(ref _importStatus, value))
            {
                OnPropertyChanged(nameof(HasImportStatus));
            }
        }
    }

    public bool HasImportStatus => ImportStatus.Length > 0;

    public DiagnosticsViewModel Diagnostics
    {
        get => _diagnostics;
        private set => SetProperty(ref _diagnostics, value);
    }

    public bool IsDirty => _documentService.IsDirty;

    public string? CurrentFilePath => _documentService.CurrentFilePath;

    /// <summary>The concise document label the header shows.</summary>
    public string DocumentStatus => CurrentFilePath is { } path
        ? Path.GetFileName(path)
        : "Unsaved project";

    /// <summary>The full path, which belongs in a tooltip rather than across the header.</summary>
    public string DocumentPath => CurrentFilePath ?? "This project has not been saved yet.";

    public string WindowTitle => $"{Project.Metadata.Name}{(IsDirty ? " *" : string.Empty)} — KeyboardStudio";

    /// <summary>
    /// Produces the content of a new document. A new document is never empty: it starts from
    /// the seed project, so the user has a working layout to modify rather than bare geometry.
    /// </summary>
    private KeyboardProject CreateProject(KeyboardTemplateDescriptor template)
    {
        var seed = _seedProjectSource.Create(SeedProjectId.Default);
        if (string.Equals(seed.Keyboard.Id, template.Id, StringComparison.Ordinal))
        {
            return seed;
        }

        // The seed is authored against one geometry. On any other template, keep the mappings
        // whose physical key exists there and drop the rest; a key the template does not have
        // would otherwise fail mapping validation.
        var keyboard = _templateProvider.Load(template.Id);
        var availableKeyIds = keyboard.Keys
            .Select(key => key.Id)
            .ToHashSet(StringComparer.Ordinal);

        return new KeyboardProject
        {
            Metadata = new ProjectMetadata
            {
                Name = $"{seed.Metadata.Name} ({template.Name})",
                Description = seed.Metadata.Description,
                Version = seed.Metadata.Version,
                Language = seed.Metadata.Language
            },
            Keyboard = keyboard,
            Layout = new KeyboardLayout
            {
                Mappings = seed.Layout.Mappings
                    .Where(mapping => availableKeyIds.Contains(mapping.KeyId))
                    .ToList()
            }
        };
    }

    private KeyboardEditorViewModel CreateEditor(
        KeyboardProject project,
        KeyboardTemplateDescriptor template) =>
        new(new KeyboardEditor(project), template, DocumentChanged);

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmDocumentReplacementAsync())
        {
            return;
        }

        ReplaceProject(_documentService.CreateNew(), _selectedTemplate);
    }

    private async Task OpenDocumentAsync()
    {
        var path = await _interactionService.SelectOpenPathAsync();
        if (path is null || !await ConfirmDocumentReplacementAsync())
        {
            return;
        }

        var result = await _documentService.OpenAsync(path);
        if (!result.Success)
        {
            await ShowOperationErrorAsync("Could not open project", result);
            return;
        }

        var project = _documentService.CurrentProject!;
        var template = Templates.FirstOrDefault(candidate => candidate.Id == project.Keyboard.Id)
            ?? _selectedTemplate;
        ReplaceProject(project, template);
        await LinuxVariant.RefreshAsync(CancellationToken.None);

        // An opened document says where it came from if it was ever imported, so provenance is
        // visible without having to open the file.
        if (_documentService.CurrentProvenance is { } provenance)
        {
            ImportStatus = $"Imported from {provenance.Describe()} on {provenance.ImportedAtUtc:yyyy-MM-dd}.";
        }
    }

    /// <summary>
    /// Replaces the starting document with the layout this host is already configured to type
    /// with, if it can be read and if the user has not started working in the meantime.
    /// </summary>
    /// <remarks>
    /// Not run from the constructor. The editor has a working document the moment it is built, and
    /// the first frame is drawn from it; detecting and importing the host's layout takes hundreds
    /// of file reads and is worth none of that delay. So this is started separately, after the
    /// window exists, and the document it produces arrives a moment later or not at all.
    ///
    /// Everything that can fail here fails quietly. Nobody asked for this import: a dialog about a
    /// layout the user never mentioned would be an interruption, so a failure leaves a diagnostics
    /// entry and the document the editor already had.
    /// </remarks>
    public async Task ImportHostLayoutAsync(CancellationToken cancellationToken = default)
    {
        if (!_importCatalog.HasAvailableSources)
        {
            // Nothing on this host can list a layout, so there is nothing to detect and nothing to
            // report either: the starting document was always going to be the only one there was.
            return;
        }

        var reference = _hostLayoutProbe.Detect();
        if (reference is null)
        {
            return;
        }

        LayoutImportResult result;
        try
        {
            // Onto the thread pool in one hop. A source composes a layout from files as it is
            // asked for it and hands back a task that is already finished, so awaiting it on the
            // UI thread would hold the window for the whole of the work rather than none of it.
            result = await Task.Run(
                () => _importCatalog.ImportAsync(reference, LayoutImportOptions.Default, cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            NoteHostLayoutUnavailable(reference, exception.Message);
            return;
        }

        if (result is not { Success: true, Project: { } imported })
        {
            NoteHostLayoutUnavailable(reference, "it could not be read on this host");
            return;
        }

        if (!IsUntouchedStartupDocument)
        {
            // The user got there first. Whatever they are working in now is what they asked for,
            // and replacing it because a background task finished would be the editor overruling
            // them on the strength of a guess.
            return;
        }

        var provenance = new LayoutImportProvenance(
            reference.SourceId,
            reference.LayoutId,
            reference.VariantId,
            reference.SourceLocation,
            imported.Metadata.Name,
            DateTimeOffset.UtcNow);

        var project = _documentService.Adopt(new KeyboardProjectDocument(
            imported,
            CreateImportedTargetProfiles(reference.LayoutId, reference.VariantId, imported.Metadata.Name),
            provenance));
        var template = Templates.FirstOrDefault(candidate => candidate.Id == project.Keyboard.Id)
            ?? _selectedTemplate;

        ReplaceProject(project, template);
        _startupProject = project;
        ImportStatus = $"Started from this host's layout, {provenance.Describe()}.";
        await LinuxVariant.RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Whether the document is still the untouched one the editor started with. The host import
    /// replaces only that: a user who has typed, opened a file, or made a new document has said
    /// what they want to work on, and having it swapped out a moment later would be worse than
    /// never importing at all.
    /// </summary>
    private bool IsUntouchedStartupDocument =>
        ReferenceEquals(Project, _startupProject) && !IsDirty && CurrentFilePath is null;

    /// <summary>
    /// Records that the host's own layout could not be imported. It goes in the diagnostics list
    /// rather than in a dialog because it explains something the user can see — the layout they
    /// type with is not the one on screen — without demanding anything of them.
    /// </summary>
    private void NoteHostLayoutUnavailable(ImportableLayoutReference reference, string reason)
    {
        var name = reference.VariantId is null
            ? reference.LayoutId
            : $"{reference.LayoutId}({reference.VariantId})";

        _hostImportIssue = new ValidationIssue(
            ValidationSeverity.Info,
            LayoutImportDiagnosticCodes.HostLayoutUnavailable,
            $"This host is configured for '{name}', but {reason}. The starting layout was kept.");
        RefreshDiagnostics();
    }

    /// <summary>
    /// Imports a layout the host advertises, from <b>File &gt; Import layout…</b> or the editor's
    /// own Import button.
    /// </summary>
    private Task ImportLayoutAsync() =>
        RunImportAsync(new LayoutImportViewModel(_importCatalog, Templates, _selectedTemplate));

    /// <summary>
    /// Imports one symbols file the user points at, for a layout no catalog lists — one they are
    /// writing themselves, or one that came with something other than the distribution.
    /// </summary>
    private async Task ImportFromFileAsync()
    {
        var path = await _interactionService.SelectSymbolsFilePathAsync();
        if (path is null)
        {
            return;
        }

        await RunImportAsync(LayoutImportViewModel.ForDescriptor(
            _importCatalog,
            Templates,
            HostLayoutImportCatalog.DescribeFile(path),
            _selectedTemplate));
    }

    /// <summary>
    /// Runs the import dialog and commits what it produced.
    ///
    /// The dialog imports but never commits: it hands back a project and a report, and the decision
    /// of what to do with the open document is taken here, where the document lives. That is also
    /// why the unsaved-changes prompt comes after the dialog rather than before it — both commit
    /// paths discard work in progress, and neither is worth prompting about until the user has said
    /// which one they want.
    /// </summary>
    private async Task RunImportAsync(LayoutImportViewModel importViewModel)
    {
        await importViewModel.LoadAsync();

        if (!await _interactionService.ShowLayoutImportAsync(importViewModel) ||
            importViewModel.Result is not { Success: true, Project: { } imported } ||
            importViewModel.SelectedDescriptor is not { } descriptor ||
            !await ConfirmDocumentReplacementAsync())
        {
            return;
        }

        var importedAt = DateTimeOffset.UtcNow;
        var provenance = new LayoutImportProvenance(
            descriptor.SourceId,
            descriptor.LayoutId,
            descriptor.VariantId,
            descriptor.SourceLocation,
            descriptor.DisplayName,
            importedAt);

        if (importViewModel.CommitMode == LayoutImportCommitMode.ReplaceMappings)
        {
            ReplaceMappingsFromImport(imported, provenance);
            return;
        }

        var project = _documentService.Adopt(new KeyboardProjectDocument(
            imported,
            CreateImportedTargetProfiles(
                descriptor.LayoutId,
                descriptor.VariantId,
                descriptor.DisplayName),
            provenance,
            LayoutDerivationFactory.Create(descriptor, importViewModel.Result, importedAt)));
        var template = Templates.FirstOrDefault(candidate => candidate.Id == project.Keyboard.Id)
            ?? _selectedTemplate;
        ReplaceProject(project, template);
        ImportStatus = $"Imported {provenance.Describe()} from {descriptor.SourceLocation}.";
        await LinuxVariant.RefreshAsync();
    }

    /// <summary>
    /// Lays an imported layout onto the open document, keeping its geometry, its build settings and
    /// the file it is saved as. Only what the keys produce changes.
    /// </summary>
    private void ReplaceMappingsFromImport(
        KeyboardProject imported,
        LayoutImportProvenance provenance)
    {
        var skipped = new KeyboardEditor(Project).ReplaceMappings(imported.Layout.Mappings);
        _documentService.RecordProvenance(provenance);
        ReplaceProject(Project, _selectedTemplate);

        // The dialog pins the geometry to this document's own, so a key that does not fit is a
        // surprise worth naming rather than a routine part of the operation.
        ImportStatus = skipped == 0
            ? $"Mappings replaced from {provenance.Describe()}."
            : $"Mappings replaced from {provenance.Describe()}; {skipped} key(s) do not exist on {_selectedTemplate.Name} and were dropped.";
    }

    /// <summary>
    /// Build settings for a freshly imported project, with the XKB profile filled in from what was
    /// imported so the layout can be built straight back out.
    /// </summary>
    /// <remarks>
    /// The generated layout ID is always suffixed and never reuses the source's own: an artifact
    /// named <c>symbols/pl</c> would shadow the distribution's file if it were copied into an XKB
    /// root. The variant becomes the section, which is where XKB keeps it.
    /// </remarks>
    private static Dictionary<string, ProjectTargetProfile> CreateImportedTargetProfiles(
        string layoutId,
        string? variantId,
        string description)
    {
        var profiles = new Dictionary<string, ProjectTargetProfile>(
            BuildViewModel.CreateDefaultTargetProfiles(),
            StringComparer.Ordinal);

        if (!profiles.TryGetValue(BuildProfileTargetIds.LinuxXkb, out var linux))
        {
            return profiles;
        }

        profiles[BuildProfileTargetIds.LinuxXkb] = new ProjectTargetProfile(
            linux.Target,
            new Dictionary<string, string>(linux.Settings, StringComparer.Ordinal)
            {
                [BuildProfileKeys.LayoutId] = XkbLayoutMetadata.SanitizeIdentifier(
                    $"{layoutId}-custom",
                    "keyboardstudio"),
                [BuildProfileKeys.SectionId] = XkbLayoutMetadata.SanitizeIdentifier(
                    variantId,
                    "basic"),
                [BuildProfileKeys.Description] = description,
                [BuildProfileKeys.UserVariantId] = XkbLayoutMetadata.SanitizeIdentifier(
                    $"keyboardstudio_{variantId ?? "custom"}",
                    "keyboardstudio_custom"),
                [BuildProfileKeys.UserVariantDescription] = $"{description} - KeyboardStudio"
            });

        return profiles;
    }

    private async Task SaveDocumentAsync()
    {
        if (CurrentFilePath is null)
        {
            await SaveAsDocumentAsync();
            return;
        }

        var result = await _documentService.SaveAsync();
        if (!result.Success)
        {
            await ShowOperationErrorAsync("Could not save project", result);
        }

        RefreshDocumentState();
    }

    private async Task SaveAsDocumentAsync()
    {
        var suggestedFileName = CreateSuggestedFileName(Project.Metadata.Name);
        var path = await _interactionService.SelectSavePathAsync(suggestedFileName);
        if (path is null)
        {
            return;
        }

        var result = await _documentService.SaveAsAsync(path);
        if (!result.Success)
        {
            await ShowOperationErrorAsync("Could not save project", result);
        }

        RefreshDocumentState();
    }

    private async Task<bool> ConfirmDocumentReplacementAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        var choice = await _interactionService.ConfirmUnsavedChangesAsync(Project.Metadata.Name);
        switch (choice)
        {
            case ProjectReplacementChoice.Discard:
                return true;
            case ProjectReplacementChoice.Save:
                await SaveDocumentAsync();
                return !IsDirty;
            default:
                return false;
        }
    }

    private void ReplaceProject(KeyboardProject project, KeyboardTemplateDescriptor template)
    {
        // Cleared here rather than at each call site, so a new or opened document never inherits
        // the line describing where the previous one was imported from, nor the note about a host
        // layout that could not be read for a document that is no longer on screen. Whoever put a
        // document in place sets them again afterwards if there is something to say.
        ImportStatus = string.Empty;
        _hostImportIssue = null;
        _selectedTemplate = template;
        OnPropertyChanged(nameof(SelectedTemplate));
        Project = project;
        Editor = CreateEditor(project, template);
        Diagnostics = CreateDiagnostics(Editor);
        Build.ApplyTargetProfiles(_documentService.CurrentTargetProfiles);
        LinuxVariant.ResetForDocument();
        RefreshDiagnostics();
        RefreshDocumentState();
    }

    private void DocumentChanged()
    {
        _documentService.MarkDirty();
        LinuxVariant.NotifyProjectChanged();
        RefreshDiagnostics();
        RefreshDocumentState();
    }

    private void BuildProfileChanged()
    {
        _documentService.UpdateTargetProfiles(Build.ExportTargetProfiles());
        RefreshDocumentState();
    }

    private void RefreshDocumentState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(DocumentStatus));
        OnPropertyChanged(nameof(DocumentPath));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private static DiagnosticsViewModel CreateDiagnostics(KeyboardEditorViewModel editor) =>
        new(keyId => editor.SelectKey(keyId));

    /// <summary>
    /// Re-runs validation and shows what it found, plus the standing note about a host layout that
    /// could not be imported. The note is folded in here rather than appended to the list, because
    /// the list is rebuilt from validation on every edit and anything merely added to it would
    /// vanish at the next keystroke. Only validation reaches the keyboard: the note concerns no
    /// key, and marking one would send the user somewhere with nothing to see.
    /// </summary>
    private void RefreshDiagnostics()
    {
        var result = _validator.Validate(Project);
        Diagnostics.Refresh(_hostImportIssue is null
            ? result
            : new ValidationResult([.. result.Issues, _hostImportIssue]));
        Editor.ApplyDiagnostics(result.Issues);
        Build?.Refresh();
    }

    private static KeyboardProjectValidator CreateDefaultValidator() =>
        new KeyboardProjectValidator([
            new MetadataValidationRule(),
            new PhysicalKeyboardValidationRule(),
            new MappingValidationRule()
        ]);

    private Task ShowOperationErrorAsync(
        string title,
        ProjectDocumentOperationResult result) =>
        _interactionService.ShowErrorAsync(
            title,
            result.Error?.Message ?? "The operation failed for an unknown reason.");

    private static string CreateSuggestedFileName(string projectName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(projectName
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray())
            .Trim();
        return $"{(string.IsNullOrEmpty(sanitized) ? "keyboard-layout" : sanitized)}.kbdproj";
    }
}
