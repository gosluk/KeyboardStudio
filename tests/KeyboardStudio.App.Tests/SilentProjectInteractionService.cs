namespace KeyboardStudio.App.Tests;

/// <summary>
/// Interaction service that answers every prompt without user input.
/// </summary>
internal sealed class SilentProjectInteractionService : IProjectInteractionService
{
    public ProjectReplacementChoice ReplacementChoice { get; init; } =
        ProjectReplacementChoice.Cancel;

    public Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName) =>
        Task.FromResult(ReplacementChoice);

    public Task<string?> SelectOpenPathAsync() => Task.FromResult<string?>(null);

    public Task<string?> SelectSavePathAsync(string suggestedFileName) =>
        Task.FromResult<string?>(null);

    public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
}
