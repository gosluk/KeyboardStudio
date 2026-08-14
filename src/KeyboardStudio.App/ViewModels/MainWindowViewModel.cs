using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        Project = DemoProjectFactory.Create();
        Editor = new KeyboardEditorViewModel(new KeyboardEditor(Project));
        Build = new BuildViewModel(new WindowsBuildEnvironment());
    }

    public KeyboardProject Project { get; }
    public KeyboardEditorViewModel Editor { get; }
    public BuildViewModel Build { get; }
}
