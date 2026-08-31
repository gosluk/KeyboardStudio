using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

/// <summary>
/// Proves appearance is not part of a document.
/// </summary>
/// <remarks>
/// A theme lives in the application's own settings file. If choosing one ever marked a project
/// dirty or changed a single byte of its serialized form, appearance would have become project
/// data — and every saved <c>.kbdproj</c> would start carrying the machine it was edited on.
/// </remarks>
public sealed class AppearanceProjectIsolationTests
{
    [Fact]
    [Trait("Category", "Golden")]
    public async Task ChangingTheThemeLeavesTheSerializedProjectByteForByteIdentical()
    {
        var store = new CountingApplicationSettingsStore();
        var themeService = new RecordingApplicationThemeService();
        var appearance = new AppearanceViewModel(store, themeService);
        var viewModel = new MainWindowViewModel(new SilentProjectInteractionService(), appearance);

        var before = await SerializeAsync(viewModel);

        await appearance.SelectAsync(ApplicationTheme.White);
        await appearance.SelectAsync(ApplicationTheme.Black);

        var after = await SerializeAsync(viewModel);

        Assert.Equal(before, after);
        Assert.False(viewModel.IsDirty);
        Assert.Null(viewModel.CurrentFilePath);
        Assert.DoesNotContain("theme", System.Text.Encoding.UTF8.GetString(after), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]> SerializeAsync(MainWindowViewModel viewModel)
    {
        using var stream = new MemoryStream();
        await new JsonKeyboardProjectDocumentStore().SaveAsync(
            new KeyboardProjectDocument(viewModel.Project, viewModel.Build.ExportTargetProfiles()),
            stream);
        return stream.ToArray();
    }
}
