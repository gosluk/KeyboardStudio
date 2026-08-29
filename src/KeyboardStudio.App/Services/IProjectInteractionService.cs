namespace KeyboardStudio.App;

public interface IProjectInteractionService
{
    Task<ProjectReplacementChoice> ConfirmUnsavedChangesAsync(string projectName);
    Task<string?> SelectOpenPathAsync();
    Task<string?> SelectSavePathAsync(string suggestedFileName);

    /// <summary>
    /// Shows the import dialog and reports whether the user accepted the import. What they chose
    /// is left on <paramref name="viewModel"/>: the dialog neither imports nor commits anything,
    /// so the caller reads the outcome from the same object it prepared.
    /// </summary>
    Task<bool> ShowLayoutImportAsync(LayoutImportViewModel viewModel);

    /// <summary>
    /// Asks for a keyboard definition file outside any installed catalog, or null if the user
    /// picked nothing.
    /// </summary>
    Task<string?> SelectSymbolsFilePathAsync();

    Task ShowErrorAsync(string title, string message);
}
