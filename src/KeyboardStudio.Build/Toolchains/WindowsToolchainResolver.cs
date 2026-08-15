using System.Diagnostics;
using Microsoft.Win32;

namespace KeyboardStudio.Build;

public sealed class WindowsToolchainResolver : IWindowsToolchainResolver
{
    public ResolvedBuildEnvironment? Resolve(BuildTarget target)
    {
        if (target != BuildTarget.WindowsX64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(target),
                target,
                "Unsupported Windows build target.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var toolsDirectory = FindMsvcToolsDirectory();
        var sdkDirectory = FindWindowsSdkDirectory();
        if (toolsDirectory is null || sdkDirectory is null)
        {
            return null;
        }

        const string architecture = "x64";
        var sdkVersion = FindSdkVersion(sdkDirectory);
        if (sdkVersion is null)
        {
            return null;
        }

        var toolBinDirectory = Path.Combine(toolsDirectory, "bin", "Hostx64", architecture);
        var sdkVersionBin = Environment.GetEnvironmentVariable("WindowsSdkVerBinPath");
        var sdkBinDirectory = string.IsNullOrWhiteSpace(sdkVersionBin)
            ? Path.Combine(sdkDirectory, "bin", sdkVersion)
            : sdkVersionBin.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var compilerPath = Path.Combine(toolBinDirectory, "cl.exe");
        var linkerPath = Path.Combine(toolBinDirectory, "link.exe");
        var resourceCompilerPath = Path.Combine(sdkBinDirectory, architecture, "rc.exe");
        var includePaths = new[]
        {
            Path.Combine(toolsDirectory, "include"),
            Path.Combine(sdkDirectory, "Include", sdkVersion, "ucrt"),
            Path.Combine(sdkDirectory, "Include", sdkVersion, "shared"),
            Path.Combine(sdkDirectory, "Include", sdkVersion, "um")
        };
        var libraryPaths = new[]
        {
            Path.Combine(toolsDirectory, "lib", architecture),
            Path.Combine(sdkDirectory, "Lib", sdkVersion, "ucrt", architecture),
            Path.Combine(sdkDirectory, "Lib", sdkVersion, "um", architecture)
        };

        if (!File.Exists(compilerPath) ||
            !File.Exists(linkerPath) ||
            !File.Exists(resourceCompilerPath) ||
            includePaths.Any(path => !Directory.Exists(path)) ||
            libraryPaths.Any(path => !Directory.Exists(path)))
        {
            return null;
        }

        return new ResolvedBuildEnvironment(
            target,
            compilerPath,
            linkerPath,
            resourceCompilerPath,
            includePaths,
            libraryPaths,
            Path.GetFileName(toolsDirectory),
            sdkVersion);
    }

    private static string? FindMsvcToolsDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("VCToolsInstallDir");
        if (IsDirectory(configured))
        {
            return NormalizeDirectory(configured!);
        }

        var installationDirectory = Environment.GetEnvironmentVariable("VSINSTALLDIR");
        if (!IsDirectory(installationDirectory))
        {
            installationDirectory = QueryVisualStudioInstallation();
        }

        if (!IsDirectory(installationDirectory))
        {
            return null;
        }

        return FindLatestVersionDirectory(Path.Combine(installationDirectory!, "VC", "Tools", "MSVC"));
    }

    private static string? FindWindowsSdkDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var configured = Environment.GetEnvironmentVariable("WindowsSdkDir");
        if (IsDirectory(configured))
        {
            return NormalizeDirectory(configured!);
        }

        using var localMachine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using var installedRoots = localMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows Kits\Installed Roots");
        var kitsRoot = installedRoots?.GetValue("KitsRoot10") as string;
        return IsDirectory(kitsRoot) ? NormalizeDirectory(kitsRoot!) : null;
    }

    private static string? FindSdkVersion(string sdkDirectory)
    {
        var configured = Environment.GetEnvironmentVariable("WindowsSDKVersion")?
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.IsNullOrWhiteSpace(configured) &&
            Directory.Exists(Path.Combine(sdkDirectory, "Include", configured)))
        {
            return configured;
        }

        return FindLatestVersionDirectory(Path.Combine(sdkDirectory, "Include")) is { } versionDirectory
            ? Path.GetFileName(versionDirectory)
            : null;
    }

    private static string? QueryVisualStudioInstallation()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vswherePath = Path.Combine(programFiles, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswherePath))
        {
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = vswherePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-latest");
        startInfo.ArgumentList.Add("-products");
        startInfo.ArgumentList.Add("*");
        startInfo.ArgumentList.Add("-requires");
        startInfo.ArgumentList.Add("Microsoft.VisualStudio.Component.VC.Tools.x86.x64");
        startInfo.ArgumentList.Add("-property");
        startInfo.ArgumentList.Add("installationPath");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && IsDirectory(output) ? output : null;
    }

    private static string? FindLatestVersionDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            return null;
        }

        return Directory.EnumerateDirectories(root)
            .Select(path => new { Path = path, Version = ParseVersion(Path.GetFileName(path)) })
            .Where(item => item.Version is not null)
            .OrderByDescending(item => item.Version)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private static Version? ParseVersion(string value) =>
        Version.TryParse(value, out var version) ? version : null;

    private static bool IsDirectory(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}
