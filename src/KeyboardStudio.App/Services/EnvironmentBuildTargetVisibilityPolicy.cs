using KeyboardStudio.Build;

namespace KeyboardStudio.App;

/// <summary>
/// The shipped policy: only the Linux XKB target is offered, unless the
/// <c>KEYBOARDSTUDIO_TARGETS</c> environment variable is set to <c>all</c>.
///
/// The escape hatch exists so the Windows path stays reachable for development and manual testing
/// without a rebuild. It is deliberately an environment variable rather than a setting: this is a
/// developer affordance, not a user preference, and it should not survive into a saved document.
/// </summary>
public sealed class EnvironmentBuildTargetVisibilityPolicy : IBuildTargetVisibilityPolicy
{
    /// <summary>Name of the environment variable that overrides the default.</summary>
    public const string VariableName = "KEYBOARDSTUDIO_TARGETS";

    /// <summary>Value of <see cref="VariableName"/> that makes every target visible.</summary>
    public const string AllTargetsValue = "all";

    private readonly bool _showsEveryTarget;

    /// <summary>Reads the override from the process environment.</summary>
    public EnvironmentBuildTargetVisibilityPolicy()
        : this(Environment.GetEnvironmentVariable(VariableName))
    {
    }

    /// <summary>
    /// Uses an explicit override value. Tests take this overload so they never mutate the process
    /// environment, which xUnit shares across parallel collections.
    /// </summary>
    public EnvironmentBuildTargetVisibilityPolicy(string? overrideValue)
    {
        _showsEveryTarget = string.Equals(
            overrideValue?.Trim(),
            AllTargetsValue,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool IsVisible(BuildTarget target) =>
        _showsEveryTarget || target == BuildTarget.LinuxXkb;
}
