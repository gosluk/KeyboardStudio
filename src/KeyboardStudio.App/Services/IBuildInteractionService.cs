namespace KeyboardStudio.App;

public interface IBuildInteractionService
{
    Task OpenDirectoryAsync(string path);

    Task ShowGeneratedTextAsync(string title, string content);

    Task CopyTextAsync(string text);
}
