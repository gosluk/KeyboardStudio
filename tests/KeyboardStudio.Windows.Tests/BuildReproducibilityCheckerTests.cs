using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildReproducibilityCheckerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompareAsync_WhenSourcesAndBinariesMatch_ReturnsSuccess()
    {
        var paths = await CreateArtifactsAsync("same", "same");
        try
        {
            var first = CreateGeneratedArtifact("source");
            var second = CreateGeneratedArtifact("source");

            var result = await new BuildReproducibilityChecker().CompareAsync(
                first,
                paths.First,
                second,
                paths.Second,
                "comparison-workspace");

            Assert.True(result.Success);
            Assert.True(result.GeneratedSourcesMatch);
            Assert.True(result.BinaryOutputsMatch);
            Assert.Equal(result.FirstArtifactSha256, result.SecondArtifactSha256);
            Assert.Equal("comparison-workspace", result.ComparisonWorkspacePath);
            Assert.Empty(result.Messages);
        }
        finally
        {
            Directory.Delete(paths.Root, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompareAsync_WhenSourcesAndBinariesDiffer_ReturnsBothDiagnostics()
    {
        var paths = await CreateArtifactsAsync("first", "second");
        try
        {
            var result = await new BuildReproducibilityChecker().CompareAsync(
                CreateGeneratedArtifact("first source"),
                paths.First,
                CreateGeneratedArtifact("second source"),
                paths.Second,
                "comparison-workspace");

            Assert.False(result.Success);
            Assert.False(result.GeneratedSourcesMatch);
            Assert.False(result.BinaryOutputsMatch);
            Assert.NotEqual(result.FirstArtifactSha256, result.SecondArtifactSha256);
            Assert.Contains(result.Messages, message => message.Code == "REPRO_SOURCE");
            Assert.Contains(result.Messages, message => message.Code == "REPRO_BINARY");
        }
        finally
        {
            Directory.Delete(paths.Root, recursive: true);
        }
    }

    private static GeneratedArtifact CreateGeneratedArtifact(string source) =>
        new(new GeneratedSource(new Dictionary<string, string>
        {
            ["keyboard.c"] = source,
            ["keyboard.def"] = "EXPORTS\n    KbdLayerDescriptor @1\n"
        }));

    private static async Task<(string Root, string First, string Second)> CreateArtifactsAsync(
        string firstContent,
        string secondContent)
    {
        var root = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var first = Path.Combine(root, "first.dll");
        var second = Path.Combine(root, "second.dll");
        await File.WriteAllTextAsync(first, firstContent);
        await File.WriteAllTextAsync(second, secondContent);
        return (root, first, second);
    }
}
