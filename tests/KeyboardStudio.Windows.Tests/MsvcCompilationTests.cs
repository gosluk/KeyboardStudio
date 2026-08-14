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
                ["keyboard.h"] = "#pragma once\n"
            }));

            var result = await compiler.CompileAsync(
                artifact,
                new BuildOptions(target, buildRoot));

            Assert.True(result.Success);
            var request = Assert.Single(runner.Requests);
            Assert.Equal(@"C:\toolchain\cl.exe", request.Executable);
            Assert.Contains("/c", request.Arguments);
            Assert.Contains(architectureDefine, request.Arguments);
            Assert.Contains(request.Arguments, argument => argument.StartsWith("/Fo", StringComparison.Ordinal));
            Assert.Contains(@"/IC:\toolchain\include", request.Arguments);
            Assert.Equal(@"C:\toolchain\include", request.Environment["INCLUDE"]);
            Assert.EndsWith("keyboard.obj", result.ArtifactPath, StringComparison.Ordinal);
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
                    ["keyboard.c"] = "invalid"
                })),
                new BuildOptions(BuildTarget.WindowsX64, buildRoot));

            Assert.False(result.Success);
            Assert.Contains(result.Messages, message => message.Code == "MSVC_CL");
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
