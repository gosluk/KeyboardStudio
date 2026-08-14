namespace KeyboardStudio.Build;

public sealed class WindowsBuildEnvironment : IBuildEnvironment
{
    public bool CanBuild(BuildTarget target) => OperatingSystem.IsWindows();

    public BuildEnvironmentStatus GetStatus(BuildTarget target) => OperatingSystem.IsWindows()
        ? new BuildEnvironmentStatus(true, "Windows host detected. MSVC/WDK discovery will be implemented next.")
        : new BuildEnvironmentStatus(false, "Native Windows keyboard DLL compilation requires a Windows build host with MSVC/WDK.");
}
