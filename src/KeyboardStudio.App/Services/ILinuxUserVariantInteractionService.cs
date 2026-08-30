namespace KeyboardStudio.App;

public interface ILinuxUserVariantInteractionService
{
    Task<bool> ConfirmLiveXkbOperationAsync(
        string action,
        IReadOnlyList<string> paths);

    Task OpenDirectoryAsync(string path);
}
