using System.Text;
using System.Text.Json;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class ProjectPersistenceTests
{
    [Fact]
    public async Task SaveAndLoad_WhenCurrentSchemaIsUsed_PreservesSchemaVersion()
    {
        var project = DemoProjectFactory.Create();
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await store.SaveAsync(project, stream);
        stream.Position = 0;

        using (var document = await JsonDocument.ParseAsync(stream))
        {
            Assert.Equal(
                KeyboardProjectSchema.CurrentVersion,
                document.RootElement.GetProperty("schemaVersion").GetInt32());
        }

        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);

        Assert.Equal(KeyboardProjectSchema.CurrentVersion, loaded.SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_WhenSchemaVersionIsMissing_ReportsMissingSchemaVersion()
    {
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream("{}");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.MissingSchemaVersion, exception.ErrorCode);
        Assert.Null(exception.SchemaVersion);
    }

    [Fact]
    public async Task LoadAsync_WhenSchemaVersionIsFuture_RejectsProject()
    {
        var futureVersion = KeyboardProjectSchema.CurrentVersion + 1;
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream($"{{\"schemaVersion\":{futureVersion}}}");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.UnsupportedFutureSchema, exception.ErrorCode);
        Assert.Equal(futureVersion, exception.SchemaVersion);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("\"1\"")]
    public async Task LoadAsync_WhenSchemaVersionIsInvalid_ReportsInvalidSchemaVersion(string schemaValue)
    {
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream($"{{\"schemaVersion\":{schemaValue}}}");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.InvalidSchemaVersion, exception.ErrorCode);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsMalformed_ReportsInvalidJson()
    {
        var store = new JsonKeyboardProjectStore();
        using var stream = CreateStream("{");

        var exception = await Assert.ThrowsAsync<ProjectLoadException>(() => store.LoadAsync(stream));

        Assert.Equal(ProjectLoadErrorCode.InvalidJson, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveAsync_WhenSchemaVersionIsNotCurrent_RejectsProject()
    {
        var source = DemoProjectFactory.Create();
        var project = new KeyboardProject
        {
            SchemaVersion = KeyboardProjectSchema.CurrentVersion + 1,
            Metadata = source.Metadata,
            Keyboard = source.Keyboard,
            Layout = source.Layout
        };
        var store = new JsonKeyboardProjectStore();
        await using var stream = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(project, stream));
    }

    private static MemoryStream CreateStream(string content) => new(Encoding.UTF8.GetBytes(content));
}
