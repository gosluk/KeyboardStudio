namespace KeyboardStudio.Build;

public interface IWindowsBuildEnvironmentProbe
{
    BuildEnvironmentStatus Probe();
    ResolvedBuildEnvironment? Resolve(BuildTarget target);
}
