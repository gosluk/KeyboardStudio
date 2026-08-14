namespace KeyboardStudio.Build;

public sealed class WindowsBuildEnvironment : IBuildEnvironment
{
    private readonly BuildEnvironmentStatus _status;

    public WindowsBuildEnvironment()
        : this(new WindowsBuildEnvironmentProbe())
    {
    }

    public WindowsBuildEnvironment(IWindowsBuildEnvironmentProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _status = probe.Probe();
    }

    public bool CanBuild(BuildTarget target) =>
        _status.Available && _status.SupportedTargets.Contains(target);

    public BuildEnvironmentStatus GetStatus(BuildTarget target)
    {
        if (_status.Available && !_status.SupportedTargets.Contains(target))
        {
            return _status with
            {
                Available = false,
                Message = $"The Windows build environment does not support {target}."
            };
        }

        return _status;
    }
}
