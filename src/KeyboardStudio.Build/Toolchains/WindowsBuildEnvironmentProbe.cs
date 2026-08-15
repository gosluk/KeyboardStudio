namespace KeyboardStudio.Build;

public sealed class WindowsBuildEnvironmentProbe : IWindowsBuildEnvironmentProbe
{
    private static readonly BuildTarget[] WindowsTargets =
    [
        BuildTarget.WindowsX64
    ];

    private readonly IReadOnlyDictionary<BuildTarget, ResolvedBuildEnvironment> _resolutions;

    public WindowsBuildEnvironmentProbe()
        : this(new WindowsToolchainResolver())
    {
    }

    public WindowsBuildEnvironmentProbe(IWindowsToolchainResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var resolutions = new Dictionary<BuildTarget, ResolvedBuildEnvironment>();
        if (OperatingSystem.IsWindows())
        {
            foreach (var target in WindowsTargets)
            {
                if (resolver.Resolve(target) is { } resolution)
                {
                    resolutions.Add(target, resolution);
                }
            }
        }

        _resolutions = resolutions;
    }

    public BuildEnvironmentStatus Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Unavailable(
                new BuildEnvironmentDiagnostic(
                    "ENV_HOST",
                    "Native Windows keyboard DLL compilation requires a Windows host."));
        }

        var supportedTargets = _resolutions.Keys.Order().ToArray();
        if (supportedTargets.Length == 0)
        {
            BuildEnvironmentDiagnostic[] diagnostics =
            [
                new("ENV_MSVC", "A complete MSVC installation with cl.exe and link.exe was not found."),
                new("ENV_SDK", "A complete Windows SDK/WDK installation with headers, libraries, and rc.exe was not found.")
            ];
            return new BuildEnvironmentStatus(
                false,
                "The Windows native build environment is incomplete.",
                diagnostics,
                supportedTargets);
        }

        return new BuildEnvironmentStatus(
            true,
            $"MSVC and Windows SDK tools are available for {string.Join(", ", supportedTargets)}.",
            [],
            supportedTargets);
    }

    public ResolvedBuildEnvironment? Resolve(BuildTarget target) =>
        _resolutions.GetValueOrDefault(target);

    private static BuildEnvironmentStatus Unavailable(BuildEnvironmentDiagnostic diagnostic) =>
        new(false, diagnostic.Message, [diagnostic], []);
}
