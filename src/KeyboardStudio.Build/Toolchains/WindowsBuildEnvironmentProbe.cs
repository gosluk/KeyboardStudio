namespace KeyboardStudio.Build;

public sealed class WindowsBuildEnvironmentProbe : IWindowsBuildEnvironmentProbe
{
    public BuildEnvironmentStatus Probe()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Unavailable(
                new BuildEnvironmentDiagnostic(
                    "ENV_HOST",
                    "Native Windows keyboard DLL compilation requires a Windows host."));
        }

        var diagnostics = new List<BuildEnvironmentDiagnostic>();
        var toolsDirectory = Environment.GetEnvironmentVariable("VCToolsInstallDir");
        var sdkDirectory = Environment.GetEnvironmentVariable("WindowsSdkDir");
        var sdkBinDirectory = Environment.GetEnvironmentVariable("WindowsSdkVerBinPath");

        if (string.IsNullOrWhiteSpace(toolsDirectory) || !Directory.Exists(toolsDirectory))
        {
            diagnostics.Add(new BuildEnvironmentDiagnostic(
                "ENV_MSVC",
                "MSVC was not found. Run from a Visual Studio developer environment or install Visual Studio Build Tools."));
        }

        if (string.IsNullOrWhiteSpace(sdkDirectory) || !Directory.Exists(sdkDirectory))
        {
            diagnostics.Add(new BuildEnvironmentDiagnostic(
                "ENV_SDK",
                "The Windows SDK/WDK was not found. Install its headers and libraries."));
        }

        var supportedTargets = new List<BuildTarget>();
        if (!string.IsNullOrWhiteSpace(toolsDirectory))
        {
            DetectTarget(toolsDirectory, sdkBinDirectory, "x64", BuildTarget.WindowsX64, supportedTargets, diagnostics);
            DetectTarget(toolsDirectory, sdkBinDirectory, "arm64", BuildTarget.WindowsArm64, supportedTargets, diagnostics);
        }

        if (diagnostics.Count > 0 || supportedTargets.Count == 0)
        {
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

    private static void DetectTarget(
        string toolsDirectory,
        string? sdkBinDirectory,
        string architecture,
        BuildTarget target,
        ICollection<BuildTarget> supportedTargets,
        ICollection<BuildEnvironmentDiagnostic> diagnostics)
    {
        var compilerPath = Path.Combine(toolsDirectory, "bin", "Hostx64", architecture, "cl.exe");
        var linkerPath = Path.Combine(toolsDirectory, "bin", "Hostx64", architecture, "link.exe");
        var resourceCompilerPath = string.IsNullOrWhiteSpace(sdkBinDirectory)
            ? string.Empty
            : Path.Combine(sdkBinDirectory, architecture, "rc.exe");

        var missingTools = new List<string>();
        if (!File.Exists(compilerPath))
        {
            missingTools.Add("cl.exe");
        }

        if (!File.Exists(linkerPath))
        {
            missingTools.Add("link.exe");
        }

        if (!File.Exists(resourceCompilerPath))
        {
            missingTools.Add("rc.exe");
        }

        if (missingTools.Count == 0)
        {
            supportedTargets.Add(target);
            return;
        }

        diagnostics.Add(new BuildEnvironmentDiagnostic(
            $"ENV_TOOLS_{architecture.ToUpperInvariant()}",
            $"{target} is unavailable because these tools are missing: {string.Join(", ", missingTools)}."));
    }

    private static BuildEnvironmentStatus Unavailable(BuildEnvironmentDiagnostic diagnostic) =>
        new(false, diagnostic.Message, [diagnostic], []);
}
