namespace KeyboardStudio.App;

internal sealed class NoOpBuildInteractionService : IBuildInteractionService
{
    public Task OpenDirectoryAsync(string path) => Task.CompletedTask;

    public Task ShowGeneratedTextAsync(string title, string content) => Task.CompletedTask;

    public Task CopyTextAsync(string text) => Task.CompletedTask;
}
