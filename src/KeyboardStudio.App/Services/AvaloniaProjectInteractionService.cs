using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace KeyboardStudio.App;

public sealed class AvaloniaProjectInteractionService :
    IProjectInteractionService,
    IBuildInteractionService,
    ILinuxUserVariantInteractionService
{
    private static readonly FilePickerFileType ProjectFileType = new("KeyboardStudio project")
    {
        Patterns = ["*.kbdproj"],
        MimeTypes = ["application/json"]
    };

    /// <summary>
    /// XKB symbols files carry no extension, so the picker cannot filter by one and offers "any
    /// file" alongside the couple of suffixes people give their own copies.
    /// </summary>
    private static readonly FilePickerFileType SymbolsFileType = new("Keyboard symbols file")
    {
        Patterns = ["*", "*.xkb", "*.symbols"]
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

    public Task<bool> ShowLayoutImportAsync(LayoutImportViewModel viewModel) =>
        new ImportLayoutDialog(viewModel).ShowDialog<bool>(_owner);

    public async Task<string?> SelectSymbolsFilePathAsync()
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import keyboard symbols file",
            AllowMultiple = false,
            FileTypeFilter = [SymbolsFileType]
        });

        return files.Count == 1 ? files[0].TryGetLocalPath() : null;
    }

    public Task ShowErrorAsync(string title, string message) =>
        new ProjectErrorDialog(title, message).ShowDialog(_owner);

    public Task<bool> ConfirmLiveXkbOperationAsync(
        string action,
        IReadOnlyList<string> paths) =>
        new LiveXkbOperationDialog(action, paths).ShowDialog<bool>(_owner);

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
