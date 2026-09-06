namespace KeyboardStudio.App;

public interface ILinuxUserVariantInteractionService
{
    Task<bool> ConfirmLiveXkbOperationAsync(
        string action,
        IReadOnlyList<string> paths);

    /// <summary>
    /// Asks whether to write keys that cannot be written completely, naming what each one costs.
    ///
    /// It is asked at the moment of the action rather than kept as a setting: the answer is only
    /// ever about the keys in front of the user, and nothing stays switched on afterwards to make
    /// the next variant lossy without anyone deciding so again.
    /// </summary>
    Task<bool> ConfirmIncompleteKeysAsync(
        string action,
        IReadOnlyList<string> losses);

    Task OpenDirectoryAsync(string path);
}
