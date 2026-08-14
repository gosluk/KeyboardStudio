using KeyboardStudio.Build;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string DefaultTemplateId = "iso-105";

    private readonly ProjectDocumentService _documentService;
    private readonly IProjectInteractionService _interactionService;
    private readonly IKeyboardTemplateProvider _templateProvider;
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
        IKeyboardTemplateProvider templateProvider,
        IProjectInteractionService interactionService)
    {
        ArgumentNullException.ThrowIfNull(templateProvider);
        ArgumentNullException.ThrowIfNull(interactionService);

        _templateProvider = templateProvider;
        _interactionService = interactionService;
        Templates = templateProvider.Templates;
        _selectedTemplate = Templates.FirstOrDefault(template => template.Id == DefaultTemplateId)
            ?? Templates[0];
        _documentService = new ProjectDocumentService(
            new JsonKeyboardProjectStore(),
            () => CreateProject(_selectedTemplate));
        _project = _documentService.CreateNew();
        _editor = CreateEditor(_project, _selectedTemplate);
        Build = new BuildViewModel(new WindowsBuildEnvironment());
        NewCommand = new AsyncRelayCommand(NewDocumentAsync);
        OpenCommand = new AsyncRelayCommand(OpenDocumentAsync);
        SaveCommand = new AsyncRelayCommand(SaveDocumentAsync);
        SaveAsCommand = new AsyncRelayCommand(SaveAsDocumentAsync);
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

    public BuildViewModel Build { get; }
    public IAsyncRelayCommand NewCommand { get; }
    public IAsyncRelayCommand OpenCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand SaveAsCommand { get; }

    public bool IsDirty => _documentService.IsDirty;

    public string? CurrentFilePath => _documentService.CurrentFilePath;

    public string DocumentStatus => CurrentFilePath ?? "Unsaved project";

    public string WindowTitle => $"{Project.Metadata.Name}{(IsDirty ? " *" : string.Empty)} — KeyboardStudio";

    private KeyboardProject CreateProject(KeyboardTemplateDescriptor template) => new()
    {
        Metadata = new ProjectMetadata
        {
            Name = $"{template.Name} layout",
            Description = "Template-driven project used by the application editor.",
            Version = "0.1.0",
            Language = "und"
        },
        Keyboard = _templateProvider.Load(template.Id),
        Layout = new KeyboardLayout()
    };

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
        _selectedTemplate = template;
        OnPropertyChanged(nameof(SelectedTemplate));
        Project = project;
        Editor = CreateEditor(project, template);
        RefreshDocumentState();
    }

    private void DocumentChanged()
    {
        _documentService.MarkDirty();
        RefreshDocumentState();
    }

    private void RefreshDocumentState()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(DocumentStatus));
        OnPropertyChanged(nameof(WindowTitle));
    }

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
