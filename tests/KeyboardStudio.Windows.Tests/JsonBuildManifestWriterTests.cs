using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KeyboardStudio.Build;
using KeyboardStudio.Core;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class JsonBuildManifestWriterTests
{
    [Fact]
    public async Task WriteAsync_RecordsSourceToolchainAndVerifiedOutput()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"KeyboardStudio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, "kbd-demo.dll");
        var outputBytes = "verified dll"u8.ToArray();
        await File.WriteAllBytesAsync(outputPath, outputBytes);
        try
        {
            var timestamp = new DateTimeOffset(2026, 8, 14, 20, 15, 0, TimeSpan.Zero);
            var source = new GeneratedSource(new Dictionary<string, string>
            {
                ["keyboard.def"] = "EXPORTS\n    KbdLayerDescriptor @1\n",
                ["keyboard.c"] = "/* deterministic */\n"
            });
            var verification = new ArtifactVerificationResult(
                true,
                BuildTarget.WindowsX64,
                "Amd64",
                true,
                true,
                new ArtifactLoadTestResult(ArtifactLoadTestStatus.Passed, "Resolved."),
                []);
            var compilation = new CompilationResult(
                true,
                outputPath,
                [],
                Verification: verification,
                ToolchainVersions: new BuildToolchainVersions("14.50", "10.0.26100"));
            var writer = new JsonBuildManifestWriter(new FixedTimeProvider(timestamp));

            var result = await writer.WriteAsync(
                DemoProjectFactory.Create(),
                new GeneratedArtifact(source, "kbd-demo.dll"),
                new BuildOptions(BuildTarget.WindowsX64, outputDirectory),
                compilation);

            Assert.Equal(Path.Combine(outputDirectory, "build-manifest.json"), result.ManifestPath);
            Assert.Equal(timestamp, result.Manifest.BuildTimestampUtc);
            Assert.Equal("Demo layout", result.Manifest.ProjectName);
            Assert.Equal("14.50", result.Manifest.Toolchain.Compiler);
            Assert.Equal(Hash(outputBytes), result.Manifest.Output.Sha256);
            Assert.Equal(
                Hash(Encoding.UTF8.GetBytes("/* deterministic */\n")),
                result.Manifest.GeneratedSources[0].Sha256);
            Assert.Equal("keyboard.c", result.Manifest.GeneratedSources[0].Path);

            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(result.ManifestPath));
            Assert.Equal("WindowsX64", json.RootElement.GetProperty("target").GetString());
            Assert.Equal(
                "Passed",
                json.RootElement
                    .GetProperty("verification")
                    .GetProperty("loadTestStatus")
                    .GetString());
            Assert.Equal(
                timestamp,
                json.RootElement.GetProperty("buildTimestampUtc").GetDateTimeOffset());
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class FixedTimeProvider(DateTimeOffset timestamp) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => timestamp;
    }
}
