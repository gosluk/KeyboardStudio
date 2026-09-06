using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// What the startup loader found: data and status, never a decision.
/// </summary>
/// <remarks>
/// The loader deliberately returns a project rather than installing one. Whether a freshly read
/// layout may replace what is on screen depends on what the user has done in the meantime, and that
/// is knowledge the document owner has and a loader does not.
/// </remarks>
public sealed record StartupLayoutResult(
    StartupLayoutStatus Status,
    ImportableLayoutReference? Reference,
    KeyboardProject? Project,
    string? FailureReason)
{
    public static StartupLayoutResult Unavailable() => new(StartupLayoutStatus.Unavailable, null, null, null);

    public static StartupLayoutResult Cancelled() => new(StartupLayoutStatus.Cancelled, null, null, null);

    public static StartupLayoutResult Imported(ImportableLayoutReference reference, KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(project);
        return new StartupLayoutResult(StartupLayoutStatus.Imported, reference, project, null);
    }

    public static StartupLayoutResult Failed(ImportableLayoutReference reference, string reason)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new StartupLayoutResult(StartupLayoutStatus.Failed, reference, null, reason);
    }
}
