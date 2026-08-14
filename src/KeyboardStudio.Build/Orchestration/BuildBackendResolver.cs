namespace KeyboardStudio.Build;

public sealed class BuildBackendResolver : IBuildBackendResolver
{
    private readonly Dictionary<BuildTarget, IBuildBackend> _backends;

    public BuildBackendResolver(IEnumerable<IBuildBackend> backends)
    {
        ArgumentNullException.ThrowIfNull(backends);

        var resolved = new Dictionary<BuildTarget, IBuildBackend>();
        foreach (var backend in backends)
        {
            ArgumentNullException.ThrowIfNull(backend);
            foreach (var target in backend.SupportedTargets)
            {
                if (!resolved.TryAdd(target, backend))
                {
                    throw new ArgumentException(
                        $"More than one build backend supports target '{target}'.",
                        nameof(backends));
                }
            }
        }

        _backends = resolved;
    }

    public IBuildBackend Resolve(BuildTarget target) =>
        _backends.TryGetValue(target, out var backend)
            ? backend
            : throw new InvalidOperationException(
                $"No build backend supports target '{target}'.");
}
