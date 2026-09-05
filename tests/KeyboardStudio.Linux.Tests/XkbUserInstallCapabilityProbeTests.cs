using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Linux;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class XkbUserInstallCapabilityProbeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProbeAsync_OnSupportedWaylandHost_ReportsManagedPathsAndCapabilities()
    {
        var environment = BaseEnvironment().Set("XDG_SESSION_TYPE", "wayland");
        var probe = Probe(environment, "xkbcli 1.13.1", listExitCode: 0);

        var result = await probe.ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ManagedInstallation, result.Mode);
        Assert.Equal(XkbSessionType.Wayland, result.SessionType);
        Assert.Equal("/home/test/.config/xkb", result.UserXkbRoot);
        Assert.Equal("/home/test/.local/state/keyboardstudio/xkb", result.StateRoot);
        Assert.True(result.PathsAreSafe);
        Assert.Equal(new Version(1, 13, 1), result.LibXkbCommonVersion);
        Assert.True(result.MeetsRecommendedVersion);
        Assert.Equal("/usr/share/X11/xkb", result.CanonicalSystemRoot);
        Assert.Equal(XkbRegistryDiscoverySupport.Available, result.RegistryDiscovery);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProbeAsync_AtMinimumVersion_AllowsManagedInstallButReportsRecommendation()
    {
        var result = await Probe(
            BaseEnvironment().Set("WAYLAND_DISPLAY", "wayland-0"),
            "1.11.0",
            listExitCode: 0).ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ManagedInstallation, result.Mode);
        Assert.False(result.MeetsRecommendedVersion);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC006");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("1.10.9")]
    [InlineData("not-a-version")]
    public async Task ProbeAsync_WhenVersionIsOldOrUnknown_IsExportOnly(string version)
    {
        var result = await Probe(
            BaseEnvironment().Set("XDG_SESSION_TYPE", "wayland"),
            version,
            listExitCode: 0).ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ExportOnly, result.Mode);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC004");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProbeAsync_OnX11_IsExportOnlyEvenWithSupportedTools()
    {
        var result = await Probe(
            BaseEnvironment().Set("XDG_SESSION_TYPE", "x11"),
            "1.13.1",
            listExitCode: 0).ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ExportOnly, result.Mode);
        Assert.Equal(XkbSessionType.X11, result.SessionType);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC001");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProbeAsync_WithRelativeXdgPath_RejectsManagedInstallation()
    {
        var environment = BaseEnvironment()
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("XDG_CONFIG_HOME", "relative/config");

        var result = await Probe(environment, "1.13.1", listExitCode: 0).ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ExportOnly, result.Mode);
        Assert.False(result.PathsAreSafe);
        Assert.Null(result.UserXkbRoot);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC002");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProbeAsync_WhenXkbCliIsMissing_IsExportOnlyWithoutRunningProcesses()
    {
        var runner = new QueueRunner([]);
        var probe = new XkbUserInstallCapabilityProbe(
            BaseEnvironment().Set("XDG_SESSION_TYPE", "wayland"),
            new StaticRootLocator(),
            new StaticCliLocator(null),
            runner,
            isLinux: true);

        var result = await probe.ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ExportOnly, result.Mode);
        Assert.Null(result.XkbCliPath);
        Assert.Empty(runner.Requests);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC003");

        // Naming the package turns the one blocker a user can act on into an instruction.
        var missingCli = result.Diagnostics.Single(diagnostic => diagnostic.Code == "KSC003");
        Assert.Contains("libxkbcommon-utils", missingCli.Message, StringComparison.Ordinal);
        Assert.Contains("libxkbcommon-tools", missingCli.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProbeAsync_WhenRegistryToolingIsUnavailable_KeepsManagedModeWithWarning()
    {
        var result = await Probe(
            BaseEnvironment().Set("XDG_SESSION_TYPE", "wayland"),
            "1.13.1",
            listExitCode: 2).ProbeAsync();

        Assert.Equal(XkbUserInstallMode.ManagedInstallation, result.Mode);
        Assert.Equal(XkbRegistryDiscoverySupport.Unavailable, result.RegistryDiscovery);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "KSC007");
    }

    private static FakeXkbEnvironment BaseEnvironment() =>
        new FakeXkbEnvironment().Set("HOME", "/home/test");

    private static XkbUserInstallCapabilityProbe Probe(
        FakeXkbEnvironment environment,
        string versionOutput,
        int listExitCode)
    {
        var runner = new QueueRunner(
        [
            Result(["--version"], versionOutput + "\n", string.Empty, 0),
            Result(["list", "--help"], string.Empty, string.Empty, listExitCode)
        ]);
        return new XkbUserInstallCapabilityProbe(
            environment,
            new StaticRootLocator(),
            new StaticCliLocator("/usr/bin/xkbcli"),
            runner,
            isLinux: true);
    }

    private static ProcessResult Result(
        IReadOnlyList<string> arguments,
        string stdout,
        string stderr,
        int exitCode) =>
        new(
            "/usr/bin/xkbcli",
            arguments,
            stdout,
            stderr,
            exitCode,
            TimeSpan.FromMilliseconds(1),
            "/tmp",
            new Dictionary<string, string?>());

    private sealed class StaticRootLocator : IXkbDataRootLocator
    {
        public IReadOnlyList<XkbDataRoot> Locate() =>
            [new("/usr/share/X11/xkb", LayoutSourceOrigin.System)];
    }

    private sealed class StaticCliLocator(string? path) : IXkbCliLocator
    {
        public string? Find() => path;
    }

    private sealed class QueueRunner(IEnumerable<ProcessResult> results) : IProcessRunner
    {
        private readonly Queue<ProcessResult> _results = new(results);

        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue() with
            {
                Executable = request.Executable,
                Arguments = request.Arguments,
                WorkingDirectory = request.WorkingDirectory
            });
        }
    }
}
