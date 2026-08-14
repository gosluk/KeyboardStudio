using KeyboardStudio.Build;
using CommunityToolkit.Mvvm.ComponentModel;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class MainWindowViewModel : ObservableObject
{
    private const string DefaultTemplateId = "iso-105";

    private readonly IKeyboardTemplateProvider _templateProvider;
    private KeyboardTemplateDescriptor _selectedTemplate;
    private KeyboardProject _project;
    private KeyboardEditorViewModel _editor;

    public MainWindowViewModel()
        : this(new KeyboardTemplateProvider())
    {
    }

    public MainWindowViewModel(IKeyboardTemplateProvider templateProvider)
    {
        ArgumentNullException.ThrowIfNull(templateProvider);

        _templateProvider = templateProvider;
        Templates = templateProvider.Templates;
        _selectedTemplate = Templates.FirstOrDefault(template => template.Id == DefaultTemplateId)
            ?? Templates[0];
        _project = CreateProject(_selectedTemplate);
        _editor = CreateEditor(_project, _selectedTemplate);
        Build = new BuildViewModel(new WindowsBuildEnvironment());
    }

    public IReadOnlyList<KeyboardTemplateDescriptor> Templates { get; }

    public KeyboardTemplateDescriptor SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (SetProperty(ref _selectedTemplate, value))
            {
                Project = CreateProject(value);
                Editor = CreateEditor(Project, value);
            }
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

    private static KeyboardEditorViewModel CreateEditor(
        KeyboardProject project,
        KeyboardTemplateDescriptor template) =>
        new(new KeyboardEditor(project), template);
}
