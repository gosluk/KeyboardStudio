using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Linux.Tests;

public sealed class KeyboardProjectDocumentStoreTests
{
    [Fact]
    public async Task SaveAndLoad_PreservesWindowsAndLinuxProfiles()
    {
        var document = new KeyboardProjectDocument(
            DemoProjectFactory.Create(),
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal)
            {
                ["windows"] = new("windows", new Dictionary<string, string>
                {
                    ["layoutId"] = "kbd-demo"
                }),
                ["linuxXkb"] = new("linuxXkb", new Dictionary<string, string>
                {
                    ["layoutId"] = "demo",
                    ["sectionId"] = "basic"
                })
            });
        var store = new JsonKeyboardProjectDocumentStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(document, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Equal(document.Project.Metadata.Name, loaded.Project.Metadata.Name);
        Assert.Equal("kbd-demo", loaded.TargetProfiles["windows"].Settings["layoutId"]);
        Assert.Equal("basic", loaded.TargetProfiles["linuxXkb"].Settings["sectionId"]);
    }
}
