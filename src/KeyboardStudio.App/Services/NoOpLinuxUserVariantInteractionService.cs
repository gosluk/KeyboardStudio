namespace KeyboardStudio.App;

internal sealed class NoOpLinuxUserVariantInteractionService : ILinuxUserVariantInteractionService
{
    public Task<bool> ConfirmLiveXkbOperationAsync(
        string action,
        IReadOnlyList<string> paths) =>
        Task.FromResult(false);

    public Task<bool> ConfirmIncompleteKeysAsync(
        string action,
        IReadOnlyList<string> losses) =>
        Task.FromResult(false);

    public Task OpenDirectoryAsync(string path) => Task.CompletedTask;
}
