using System.Text;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class JsonApplicationSettingsStoreTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenFileIsMissing_ReturnsGrayWithoutCreatingTheFile()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        var store = CreateStore(path);

        var result = await store.LoadAsync();

        Assert.True(result.Success);
        Assert.Equal(ApplicationTheme.Gray, result.Settings.Theme);
        Assert.Equal(ApplicationSettings.CurrentSchemaVersion, result.Settings.SchemaVersion);
        Assert.False(File.Exists(path));
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    [Theory]
    [InlineData("white", ApplicationTheme.White)]
    [InlineData("gray", ApplicationTheme.Gray)]
    [InlineData("black", ApplicationTheme.Black)]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenFileNamesASupportedTheme_LoadsThatTheme(
        string identifier,
        ApplicationTheme expected)
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        await File.WriteAllTextAsync(
            path,
            $$"""{"schemaVersion": 1, "theme": "{{identifier}}"}""");
        var store = CreateStore(path);

        var result = await store.LoadAsync();

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(expected, result.Settings.Theme);
    }

    [Theory]
    [InlineData(ApplicationTheme.White, "white")]
    [InlineData(ApplicationTheme.Gray, "gray")]
    [InlineData(ApplicationTheme.Black, "black")]
    [Trait("Category", "Unit")]
    public async Task SaveAsync_WhenThemeIsSelected_RoundTripsThroughStableLowerCaseIdentifiers(
        ApplicationTheme theme,
        string expectedIdentifier)
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        var store = CreateStore(path);

        var saveResult = await store.SaveAsync(new ApplicationSettings(1, theme));

        Assert.True(saveResult.Success);
        Assert.Null(saveResult.Error);

        var contents = await File.ReadAllTextAsync(path);
        Assert.Contains($"\"{expectedIdentifier}\"", contents, StringComparison.Ordinal);
        Assert.DoesNotContain(theme.ToString(), contents, StringComparison.Ordinal);

        var loadResult = await store.LoadAsync();

        Assert.True(loadResult.Success);
        Assert.Equal(theme, loadResult.Settings.Theme);
        Assert.Equal(ApplicationSettings.CurrentSchemaVersion, loadResult.Settings.SchemaVersion);
    }

    [Theory]
    [InlineData("{ not json", ApplicationSettingsErrorKind.InvalidData)]
    [InlineData("[]", ApplicationSettingsErrorKind.InvalidData)]
    [InlineData("""{"theme": "black"}""", ApplicationSettingsErrorKind.InvalidData)]
    [InlineData("""{"schemaVersion": 1}""", ApplicationSettingsErrorKind.InvalidData)]
    [InlineData("""{"schemaVersion": 1, "theme": 3}""", ApplicationSettingsErrorKind.InvalidData)]
    [InlineData("""{"schemaVersion": 1, "theme": "Black"}""", ApplicationSettingsErrorKind.UnknownTheme)]
    [InlineData("""{"schemaVersion": 1, "theme": "solarized"}""", ApplicationSettingsErrorKind.UnknownTheme)]
    [InlineData("""{"schemaVersion": 2, "theme": "black"}""", ApplicationSettingsErrorKind.UnsupportedSchema)]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task LoadAsync_WhenFileCannotBeUnderstood_FallsBackToGrayAndPreservesTheFile(
        string contents,
        ApplicationSettingsErrorKind expectedKind)
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        await File.WriteAllTextAsync(path, contents);
        var store = CreateStore(path);

        var result = await store.LoadAsync();

        Assert.False(result.Success);
        Assert.Equal(expectedKind, result.Error?.Kind);
        Assert.Equal(ApplicationTheme.Gray, result.Settings.Theme);
        Assert.Equal(contents, await File.ReadAllTextAsync(path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task LoadAsync_WhenPathIsInvalid_ReturnsDefaultsWithoutThrowing()
    {
        var store = CreateStore("   ");

        var result = await store.LoadAsync();

        Assert.False(result.Success);
        Assert.Equal(ApplicationSettingsErrorKind.InvalidPath, result.Error?.Kind);
        Assert.Equal(ApplicationTheme.Gray, result.Settings.Theme);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SaveAsync_WhenPathIsInvalid_ReturnsFailureWithoutThrowing()
    {
        var store = CreateStore(string.Empty);

        var result = await store.SaveAsync(ApplicationSettings.Default);

        Assert.False(result.Success);
        Assert.Equal(ApplicationSettingsErrorKind.InvalidPath, result.Error?.Kind);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SaveAsync_WhenTheParentDirectoryIsBlocked_ReturnsFailureWithoutThrowing()
    {
        using var directory = new TemporarySettingsDirectory();
        var blockingFile = directory.Combine("KeyboardStudio");
        await File.WriteAllTextAsync(blockingFile, "not a directory");
        var store = CreateStore(Path.Combine(blockingFile, "settings.json"));

        var result = await store.SaveAsync(ApplicationSettings.Default);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal("not a directory", await File.ReadAllTextAsync(blockingFile));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAsync_WhenSaveSucceeds_LeavesNoTemporaryFile()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("nested", "settings.json");
        var store = CreateStore(path);

        var result = await store.SaveAsync(new ApplicationSettings(1, ApplicationTheme.White));

        Assert.True(result.Success);
        var written = Directory.GetFileSystemEntries(Path.GetDirectoryName(path)!);
        Assert.Equal(new[] { path }, written);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SaveAsync_WhenReplacementFails_KeepsTheLastCompleteSettingsFile()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        var store = CreateStore(path);
        await store.SaveAsync(new ApplicationSettings(1, ApplicationTheme.Black));
        var previousContents = await File.ReadAllTextAsync(path);

        var interrupted = new JsonApplicationSettingsStore(
            new FixedApplicationSettingsPathProvider(path),
            new FailingReplacementSettingsFileSystem());

        var result = await interrupted.SaveAsync(new ApplicationSettings(1, ApplicationTheme.White));

        Assert.False(result.Success);
        Assert.Equal(ApplicationSettingsErrorKind.Io, result.Error?.Kind);
        Assert.Equal(previousContents, await File.ReadAllTextAsync(path));
        Assert.Equal(new[] { path }, Directory.GetFileSystemEntries(directory.Path));

        var reloaded = await store.LoadAsync();
        Assert.True(reloaded.Success);
        Assert.Equal(ApplicationTheme.Black, reloaded.Settings.Theme);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SaveAsync_WhenSchemaVersionIsUnsupported_ReturnsFailureWithoutWriting()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        var store = CreateStore(path);

        var result = await store.SaveAsync(new ApplicationSettings(2, ApplicationTheme.White));

        Assert.False(result.Success);
        Assert.Equal(ApplicationSettingsErrorKind.UnsupportedSchema, result.Error?.Kind);
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "ErrorPath")]
    public async Task SaveAsync_WhenThemeIsNotDefined_ReturnsFailureWithoutWriting()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        var store = CreateStore(path);

        var result = await store.SaveAsync(new ApplicationSettings(1, (ApplicationTheme)99));

        Assert.False(result.Success);
        Assert.Equal(ApplicationSettingsErrorKind.UnknownTheme, result.Error?.Kind);
        Assert.Empty(Directory.GetFileSystemEntries(directory.Path));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task LoadAsync_WhenCancelled_DoesNotReportAFalseSuccess()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        await File.WriteAllTextAsync(path, """{"schemaVersion": 1, "theme": "black"}""");
        var store = CreateStore(path);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.LoadAsync(cancellation.Token));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAsync_WhenSaveSucceeds_WritesUtf8JsonWithoutABom()
    {
        using var directory = new TemporarySettingsDirectory();
        var path = directory.Combine("settings.json");
        var store = CreateStore(path);

        await store.SaveAsync(new ApplicationSettings(1, ApplicationTheme.Gray));

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Contains("\"schemaVersion\": 1", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    private static JsonApplicationSettingsStore CreateStore(string path) =>
        new(new FixedApplicationSettingsPathProvider(path));
}
