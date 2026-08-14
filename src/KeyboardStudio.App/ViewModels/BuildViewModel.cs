using KeyboardStudio.Build;

namespace KeyboardStudio.App;

public sealed class BuildViewModel
{
    public BuildViewModel(IBuildEnvironment environment)
    {
        var status = environment.GetStatus(BuildTarget.WindowsX64);
        Status = status.Message;
    }

    public string Status { get; }
}
