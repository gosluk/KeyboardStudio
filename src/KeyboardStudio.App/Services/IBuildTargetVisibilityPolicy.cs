using KeyboardStudio.Build;

namespace KeyboardStudio.App;

/// <summary>
/// Decides which build targets the Build card offers.
///
/// This is presentation policy only. A hidden target is still registered, still validated, still
/// carries its own profile, and is still persisted with the document; hiding it only keeps it out
/// of the target selector so a Linux build of the app does not advertise a toolchain it cannot run.
/// </summary>
public interface IBuildTargetVisibilityPolicy
{
    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="target"/> should appear in the UI.
    /// </summary>
    bool IsVisible(BuildTarget target);
}
