namespace KeyboardStudio.Build;

public interface IBuildBackendResolver
{
    IBuildBackend Resolve(BuildTarget target);
}
