namespace KeyboardStudio.App;

public interface IProjectInteractionService
{
    Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName);
    Task<string?> SelectOpenPathAsync();
    Task<string?> SelectSavePathAsync(string suggestedFileName);
    Task ShowErrorAsync(string title, string message);
}
