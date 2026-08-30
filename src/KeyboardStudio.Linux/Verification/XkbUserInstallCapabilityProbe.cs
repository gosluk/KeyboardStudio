using System.Text.RegularExpressions;
using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>Reports whether this host can safely verify and use a per-user libxkbcommon root.</summary>
public sealed partial class XkbUserInstallCapabilityProbe : IXkbUserInstallCapabilityProbe
{
    public static readonly Version MinimumVersion = new(1, 11, 0);
    public static readonly Version RecommendedVersion = new(1, 12, 2);

    private readonly IXkbEnvironment _environment;
    private readonly IXkbDataRootLocator _rootLocator;
    private readonly IXkbCliLocator _xkbCliLocator;
    private readonly IProcessRunner _processRunner;
    private readonly bool _isLinux;
    private readonly XdgDirectoryResolver _directoryResolver;

    public XkbUserInstallCapabilityProbe(
        IXkbEnvironment environment,
        IXkbDataRootLocator rootLocator,
        IXkbCliLocator xkbCliLocator,
        IProcessRunner processRunner,
        bool? isLinux = null)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _rootLocator = rootLocator ?? throw new ArgumentNullException(nameof(rootLocator));
        _xkbCliLocator = xkbCliLocator ?? throw new ArgumentNullException(nameof(xkbCliLocator));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _isLinux = isLinux ?? OperatingSystem.IsLinux();
        _directoryResolver = new XdgDirectoryResolver(environment);
    }

    public async Task<XkbUserInstallCapability> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<XkbDiagnostic>();
        var session = DetectSession();
        var directoryResolution = _directoryResolver.Resolve();
        diagnostics.AddRange(directoryResolution.Diagnostics);
        var userXkbRoot = directoryResolution.Paths?.UserXkbRoot;
        var stateRoot = directoryResolution.Paths?.KeyboardStudioStateRoot;
        var pathsAreSafe = directoryResolution.Success;
        var canonicalRoot = _rootLocator.Locate()
            .FirstOrDefault(root => root.Origin == LayoutSourceOrigin.System)?.Path;
        if (canonicalRoot is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSC005",
                "No canonical system XKB root is available for inherited definitions."));
        }

        var executable = _xkbCliLocator.Find();
        string? versionOutput = null;
        Version? version = null;
        var registry = XkbRegistryDiscoverySupport.Unknown;
        if (executable is null)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSC003",
                "xkbcli is not available; the bundle can be exported but managed installation cannot be verified."));
        }
        else
        {
            try
            {
                var workingDirectory = Path.GetTempPath();
                var versionResult = await _processRunner.RunAsync(
                    new ProcessRequest(executable, ["--version"], workingDirectory, EmptyEnvironment()),
                    cancellationToken);
                versionOutput = SelectOutput(versionResult);
                version = ParseVersion(versionOutput);
                if (version is null)
                {
                    diagnostics.Add(new XkbDiagnostic(
                        "KSC004",
                        $"The libxkbcommon version could not be determined from '{versionOutput}'."));
                }
                else if (version < MinimumVersion)
                {
                    diagnostics.Add(new XkbDiagnostic(
                        "KSC004",
                        $"libxkbcommon {version} is older than the required {MinimumVersion}."));
                }
                else if (version < RecommendedVersion)
                {
                    diagnostics.Add(new XkbDiagnostic(
                        "KSC006",
                        $"libxkbcommon {version} supports %S includes, but {RecommendedVersion} or newer is recommended."));
                }

                var listResult = await _processRunner.RunAsync(
                    new ProcessRequest(executable, ["list", "--help"], workingDirectory, EmptyEnvironment()),
                    cancellationToken);
                registry = listResult.ExitCode == 0
                    ? XkbRegistryDiscoverySupport.Available
                    : XkbRegistryDiscoverySupport.Unavailable;
                if (registry == XkbRegistryDiscoverySupport.Unavailable)
                {
                    diagnostics.Add(new XkbDiagnostic(
                        "KSC007",
                        "xkbcli cannot query libxkbregistry; desktop discovery can be installed only with a warning."));
                }
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                registry = XkbRegistryDiscoverySupport.Unavailable;
                diagnostics.Add(new XkbDiagnostic(
                    "KSC003",
                    $"xkbcli could not be executed: {exception.Message}"));
            }
        }

        if (session != XkbSessionType.Wayland)
        {
            diagnostics.Add(new XkbDiagnostic(
                "KSC001",
                session == XkbSessionType.X11
                    ? "The active session is X11; per-user libxkbcommon installation is export-only."
                    : "A Wayland session was not detected; per-user installation is export-only."));
        }

        var mode = _isLinux &&
                   session == XkbSessionType.Wayland &&
                   pathsAreSafe &&
                   executable is not null &&
                   version is not null &&
                   version >= MinimumVersion &&
                   canonicalRoot is not null
            ? XkbUserInstallMode.ManagedInstallation
            : XkbUserInstallMode.ExportOnly;

        return new XkbUserInstallCapability(
            mode,
            session,
            userXkbRoot,
            stateRoot,
            pathsAreSafe,
            executable,
            versionOutput,
            version,
            version is not null && version >= RecommendedVersion,
            canonicalRoot,
            registry,
            diagnostics.AsReadOnly());
    }

    private XkbSessionType DetectSession()
    {
        var declared = _environment.GetVariable("XDG_SESSION_TYPE")?.Trim();
        if (string.Equals(declared, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return XkbSessionType.Wayland;
        }

        if (string.Equals(declared, "x11", StringComparison.OrdinalIgnoreCase))
        {
            return XkbSessionType.X11;
        }

        if (!string.IsNullOrWhiteSpace(_environment.GetVariable("WAYLAND_DISPLAY")))
        {
            return XkbSessionType.Wayland;
        }

        if (!string.IsNullOrWhiteSpace(_environment.GetVariable("DISPLAY")))
        {
            return XkbSessionType.X11;
        }

        return declared is null ? XkbSessionType.Headless : XkbSessionType.Unknown;
    }

    private static Version? ParseVersion(string output)
    {
        var match = VersionPattern().Match(output);
        return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
    }

    private static string SelectOutput(ProcessResult result) =>
        (string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardError
            : result.StandardOutput).Trim();

    private static Dictionary<string, string?> EmptyEnvironment() =>
        new(StringComparer.Ordinal);

    [GeneratedRegex(@"\d+\.\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
