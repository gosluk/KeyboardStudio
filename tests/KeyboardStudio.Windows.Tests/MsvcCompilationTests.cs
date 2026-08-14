using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class MsvcCompilationTests
{
    [Theory]
    [InlineData(BuildTarget.WindowsX64, "/D_WIN64")]
    [InlineData(BuildTarget.WindowsArm64, "/D_ARM64_")]
    public async Task CompileAsync_ConstructsExpectedArchitectureCommand(
        BuildTarget target,
        string architectureDefine)
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var runner = new RecordingProcessRunner();
            var compiler = new MsvcKeyboardCompiler(new ResolvedEnvironment(target), runner);
            var artifact = new GeneratedArtifact(new GeneratedSource(new Dictionary<string, string>
            {
                ["keyboard.c"] = "/* source */\n",
                ["keyboard.def"] = "EXPORTS\n",
                ["keyboard.h"] = "#pragma once\n",
                ["keyboard.rc"] = "1 VERSIONINFO\n"
            }), "kbd-demo.dll");

            var result = await compiler.CompileAsync(
                artifact,
                new BuildOptions(target, buildRoot));

            Assert.True(result.Success);
            Assert.Equal(3, runner.Requests.Count);
            var compileRequest = runner.Requests[0];
            Assert.Equal(@"C:\toolchain\cl.exe", compileRequest.Executable);
            Assert.Contains("/c", compileRequest.Arguments);
            Assert.Contains(architectureDefine, compileRequest.Arguments);
            Assert.Contains(compileRequest.Arguments, argument => argument.StartsWith("/Fo", StringComparison.Ordinal));
            Assert.Contains(@"/IC:\toolchain\include", compileRequest.Arguments);
            Assert.Equal(@"C:\toolchain\include", compileRequest.Environment["INCLUDE"]);

            var resourceRequest = runner.Requests[1];
            Assert.Equal(@"C:\sdk\rc.exe", resourceRequest.Executable);
            Assert.EndsWith("keyboard.rc", resourceRequest.Arguments[^1], StringComparison.Ordinal);

            var linkRequest = runner.Requests[2];
            Assert.Equal(@"C:\toolchain\link.exe", linkRequest.Executable);
            Assert.Contains(
                target == BuildTarget.WindowsX64 ? "/MACHINE:X64" : "/MACHINE:ARM64",
                linkRequest.Arguments);
            Assert.Contains(linkRequest.Arguments, argument => argument.EndsWith("keyboard.def", StringComparison.Ordinal));
            Assert.EndsWith("kbd-demo.dll", result.ArtifactPath, StringComparison.Ordinal);
            Assert.Contains(@"C:\toolchain\cl.exe", result.RawLog, StringComparison.Ordinal);
            Assert.NotNull(result.LogPath);
            Assert.True(File.Exists(result.LogPath));
        }
        finally
        {
            if (Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CompileAsync_WhenCompilerFails_ReturnsDiagnostic()
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var runner = new RecordingProcessRunner(exitCode: 2, standardError: "keyboard.c(1): error C1000");
            var compiler = new MsvcKeyboardCompiler(
                new ResolvedEnvironment(BuildTarget.WindowsX64),
                runner);

            var result = await compiler.CompileAsync(
                new GeneratedArtifact(new GeneratedSource(new Dictionary<string, string>
                {
                    ["keyboard.c"] = "invalid",
                    ["keyboard.def"] = "EXPORTS\n",
                    ["keyboard.rc"] = "1 VERSIONINFO\n"
                })),
                new BuildOptions(BuildTarget.WindowsX64, buildRoot));

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Messages);
            Assert.Equal("C1000", diagnostic.Code);
            Assert.Equal("keyboard.c", diagnostic.FilePath);
            Assert.NotEmpty(result.RawLog);
            Assert.True(File.Exists(result.LogPath));
        }
        finally
        {
            if (Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }

    private sealed class ResolvedEnvironment(BuildTarget target) : IBuildEnvironment
    {
        public bool CanBuild(BuildTarget requestedTarget) => requestedTarget == target;

        public BuildEnvironmentStatus GetStatus(BuildTarget requestedTarget) =>
            new(true, "Available", [], [target]);

        public ResolvedBuildEnvironment? Resolve(BuildTarget requestedTarget) =>
            requestedTarget == target
                ? new ResolvedBuildEnvironment(
                    target,
                    @"C:\toolchain\cl.exe",
                    @"C:\toolchain\link.exe",
                    @"C:\sdk\rc.exe",
                    [@"C:\toolchain\include"],
                    [@"C:\toolchain\lib"],
                    "14.50",
                    "10.0")
                : null;
    }

    private sealed class RecordingProcessRunner(
        int exitCode = 0,
        string standardOutput = "",
        string standardError = "") : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<ProcessResult> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new ProcessResult(
                request.Executable,
                request.Arguments,
                standardOutput,
                standardError,
                exitCode,
                TimeSpan.FromMilliseconds(1)));
        }
    }
}
