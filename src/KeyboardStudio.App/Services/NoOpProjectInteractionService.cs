namespace KeyboardStudio.App;

internal sealed class NoOpProjectInteractionService : IProjectInteractionService
{
    public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
        Task.FromResult(ProjectReplacementChoice.Cancel);

    public Task<string?> SelectOpenPathAsync() => Task.FromResult<string?>(null);

    public Task<string?> SelectSavePathAsync(string suggestedFileName) =>
        Task.FromResult<string?>(null);

    public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
}
