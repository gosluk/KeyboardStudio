namespace KeyboardStudio.Build;

public interface IWindowsToolchainResolver
{
    ResolvedBuildEnvironment? Resolve(BuildTarget target);
}
