namespace KeyboardStudio.Build;

public sealed class WindowsBuildEnvironment : IBuildEnvironment
{
    private readonly IWindowsBuildEnvironmentProbe _probe;
    private readonly BuildEnvironmentStatus _status;

    public WindowsBuildEnvironment()
        : this(new WindowsBuildEnvironmentProbe())
    {
    }

    public WindowsBuildEnvironment(IWindowsBuildEnvironmentProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
        _status = probe.Probe();
    }

    public bool CanBuild(BuildTarget target) =>
        _status.Available && _status.SupportedTargets.Contains(target);

    public BuildEnvironmentStatus GetStatus(BuildTarget target)
    {
        if (_status.Available && !_status.SupportedTargets.Contains(target))
        {
            var diagnostics = _status.Diagnostics
                .Append(new BuildEnvironmentDiagnostic(
                    "ENV_TARGET",
                    $"The resolved Windows toolchain does not support {target}."))
                .ToArray();
            return _status with
            {
                Available = false,
                Message = $"The Windows build environment does not support {target}.",
                Diagnostics = diagnostics
            };
        }

        return _status;
    }

    public ResolvedBuildEnvironment? Resolve(BuildTarget target) =>
        CanBuild(target) ? _probe.Resolve(target) : null;
}
