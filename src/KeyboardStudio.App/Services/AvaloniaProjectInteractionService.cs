using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace KeyboardStudio.App;

public sealed class AvaloniaProjectInteractionService : IProjectInteractionService, IBuildInteractionService
{
    private static readonly FilePickerFileType ProjectFileType = new("KeyboardStudio project")
    {
        Patterns = ["*.kbdproj"],
        MimeTypes = ["application/json"]
    };

    private readonly Window _owner;

    public AvaloniaProjectInteractionService(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
        new UnsavedChangesDialog(projectName).ShowDialog<ProjectReplacementChoice>(_owner);

    public async Task<string?> SelectOpenPathAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open KeyboardStudio project",
            AllowMultiple = false,
            FileTypeFilter = [ProjectFileType]
        });

        return files.Count == 1 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SelectSavePathAsync(string suggestedFileName)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save KeyboardStudio project",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "kbdproj",
            FileTypeChoices = [ProjectFileType],
            ShowOverwritePrompt = true
        });

        return file?.TryGetLocalPath();
    }

    public Task ShowErrorAsync(string title, string message) =>
        new ProjectErrorDialog(title, message).ShowDialog(_owner);

    public async Task OpenDirectoryAsync(string path)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if (!directory.Exists)
        {
            throw new DirectoryNotFoundException($"Build output directory '{directory.FullName}' does not exist.");
        }

        if (!await _owner.Launcher.LaunchDirectoryInfoAsync(directory))
        {
            throw new InvalidOperationException("The desktop could not open the build output directory.");
        }
    }

    public Task ShowGeneratedTextAsync(string title, string content) =>
        new GeneratedTextDialog(title, content).ShowDialog(_owner);

    public async Task CopyTextAsync(string text)
    {
        if (_owner.Clipboard is null)
        {
            throw new InvalidOperationException("The desktop clipboard is unavailable.");
        }

        await _owner.Clipboard.SetTextAsync(text);
    }
}
