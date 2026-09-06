using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Build;

namespace KeyboardStudio.App;

/// <param name="KeyId">
/// The physical key the problem is about, when it is about one. A build refuses keys one at a
/// time — a key whose output has no equivalent in the target, a key the source would not survive
/// being overridden — and naming it here is what lets the row be a way to reach it.
/// </param>
public sealed record BuildProblemViewModel(
    BuildProblemKind Kind,
    string Category,
    BuildDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? KeyId = null)
{
    public bool HasKey => KeyId is not null;

    public string KeyAssociation => KeyId is null ? string.Empty : $"Key: {KeyId}";

    /// <summary>
    /// Goes to the key this problem names, and does nothing when it names none. The row carries
    /// its own command rather than reaching up the visual tree for one, so what it does can be
    /// tested without a window.
    /// </summary>
    public IRelayCommand SelectCommand { get; init; } = new RelayCommand(() => { });
}
