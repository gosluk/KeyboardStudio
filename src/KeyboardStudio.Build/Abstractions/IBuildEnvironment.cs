namespace KeyboardStudio.Build;

public interface IBuildEnvironment
{
    bool CanBuild(BuildTarget target);
    BuildEnvironmentStatus GetStatus(BuildTarget target);
    ResolvedBuildEnvironment? Resolve(BuildTarget target);
}
