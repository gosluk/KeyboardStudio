using KeyboardStudio.App;

namespace KeyboardStudio.App.Tests;

internal sealed class FakeLinuxUserVariantInteractionService : ILinuxUserVariantInteractionService
{
    public bool Confirm { get; set; } = true;

    public string? LastAction { get; private set; }

    public IReadOnlyList<string> LastPaths { get; private set; } = [];

    public string? OpenedPath { get; private set; }

    public Task<bool> ConfirmLiveXkbOperationAsync(
        string action,
        IReadOnlyList<string> paths)
    {
        LastAction = action;
        LastPaths = paths;
        return Task.FromResult(Confirm);
    }

    public bool ConfirmIncompleteKeys { get; set; } = true;

    public IReadOnlyList<string> LastLosses { get; private set; } = [];

    public int IncompleteKeyPrompts { get; private set; }

    public Task<bool> ConfirmIncompleteKeysAsync(
        string action,
        IReadOnlyList<string> losses)
    {
        LastAction = action;
        LastLosses = losses;
        IncompleteKeyPrompts++;
        return Task.FromResult(ConfirmIncompleteKeys);
    }

    public Task OpenDirectoryAsync(string path)
    {
        OpenedPath = path;
        return Task.CompletedTask;
    }
}
