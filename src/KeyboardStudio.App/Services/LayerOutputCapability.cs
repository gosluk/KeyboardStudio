using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

/// <summary>
/// Says, for one layer row, whether the targets this installation can build would accept the
/// assignment the user is making.
/// </summary>
/// <remarks>
/// This steers rather than forbids. What a key may produce is a property of the build target, not of
/// the key. Blocking the control would leave imported layouts — which routinely contain exactly
/// these combinations — visible but uneditable. So the assignment is always allowed and the cost of
/// it is named instead.
///
/// The verdicts mirror <see cref="KeyboardStudio.Linux.XkbKeysymMapper"/>, which remains the
/// authority at build time. This is the same judgement moved earlier, to the moment the choice is
/// made.
/// </remarks>
public static class LayerOutputCapability
{
    /// <summary>
    /// Describes the first limitation <paramref name="targets"/> place on <paramref name="output"/>,
    /// or <see langword="null"/> when every target can build it.
    /// </summary>
    public static string? Describe(
        IReadOnlyCollection<BuildTarget> targets,
        LogicalKey logicalKey,
        ModifierLayer layer,
        KeyOutput? output)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (output is null or NoOutput)
        {
            return null;
        }

        foreach (var target in targets)
        {
            var warning = target switch
            {
                BuildTarget.LinuxXkb => DescribeXkb(output),
                _ => null
            };

            if (warning is not null)
            {
                return warning;
            }
        }

        return null;
    }

    private static string? DescribeXkb(KeyOutput output) =>
        output is SpecialKeyOutput { Key: LogicalKey.None }
            ? "Choose a key for this layer, or set it to No output."
            : null;
}
