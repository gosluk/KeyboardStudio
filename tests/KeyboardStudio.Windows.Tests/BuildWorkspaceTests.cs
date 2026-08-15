using KeyboardStudio.Build;
using Xunit;

namespace KeyboardStudio.Windows.Tests;

public sealed class BuildWorkspaceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Create_ProducesIsolatedDirectoryLayoutAndWritesSource()
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var first = BuildWorkspace.Create(buildRoot);
            var second = BuildWorkspace.Create(buildRoot);
            var source = new GeneratedSource(new Dictionary<string, string>
            {
                ["keyboard.c"] = "/* source */\n",
                ["keyboard.h"] = "#pragma once\n"
            });

            await first.WriteGeneratedSourceAsync(source);

            Assert.NotEqual(first.RootDirectory, second.RootDirectory);
            Assert.True(Directory.Exists(first.GeneratedDirectory));
            Assert.True(Directory.Exists(first.ObjectDirectory));
            Assert.True(Directory.Exists(first.OutputDirectory));
            Assert.True(Directory.Exists(first.LogsDirectory));
            Assert.Equal("/* source */\n", await File.ReadAllTextAsync(
                Path.Combine(first.GeneratedDirectory, "keyboard.c")));
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
    [Trait("Category", "Unit")]
    public async Task WriteGeneratedSourceAsync_RejectsDirectoryTraversal()
    {
        var buildRoot = Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}");
        try
        {
            var workspace = BuildWorkspace.Create(buildRoot);
            var source = new GeneratedSource(new Dictionary<string, string>
            {
                ["../keyboard.c"] = "invalid"
            });

            await Assert.ThrowsAsync<ArgumentException>(() => workspace.WriteGeneratedSourceAsync(source));
        }
        finally
        {
            if (Directory.Exists(buildRoot))
            {
                Directory.Delete(buildRoot, recursive: true);
            }
        }
    }
}
