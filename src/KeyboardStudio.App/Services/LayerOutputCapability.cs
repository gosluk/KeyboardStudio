using KeyboardStudio.Build;
using KeyboardStudio.Core;
using KeyboardStudio.Windows;

namespace KeyboardStudio.App;

/// <summary>
/// Says, for one layer row, whether the targets this installation can build would accept the
/// assignment the user is making.
/// </summary>
/// <remarks>
/// This steers rather than forbids. What a key may produce is a property of the build target, not of
/// the key: XKB will take a character on any key, Windows v1 will not, and the visible targets differ
/// by host. Blocking the control would forbid on one platform what another fully supports, and would
/// leave imported layouts — which routinely contain exactly these combinations — visible but
/// uneditable. So the assignment is always allowed and the cost of it is named instead.
///
/// The verdicts mirror <see cref="WindowsCompatibilityValidationRule"/> and
/// <see cref="KeyboardStudio.Linux.XkbKeysymMapper"/>, which remain the authority at build time.
/// This is the same judgement moved earlier, to the moment the choice is made.
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
                BuildTarget.WindowsX64 => DescribeWindows(logicalKey, layer, output),
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

    private static string? DescribeWindows(LogicalKey logicalKey, ModifierLayer layer, KeyOutput output)
    {
        if (logicalKey == LogicalKey.None)
        {
            return "Windows needs this key to have a logical key before it can produce anything.";
        }

        return output switch
        {
            CharacterOutput character => DescribeWindowsCharacter(logicalKey, character),
            SpecialKeyOutput special => DescribeWindowsSpecialKey(logicalKey, layer, special),
            _ => null
        };
    }

    private static string? DescribeWindowsCharacter(LogicalKey logicalKey, CharacterOutput character)
    {
        if (!WindowsLogicalKeyClassifier.ProducesCharacters(logicalKey))
        {
            return $"Windows cannot type a character from the {new LogicalKeyOptionViewModel(logicalKey).Label} key.";
        }

        return character.Value.EnumerateRunes().First().IsBmp
            ? null
            : "Windows needs ligature support for this character.";
    }

    private static string? DescribeWindowsSpecialKey(
        LogicalKey logicalKey,
        ModifierLayer layer,
        SpecialKeyOutput special)
    {
        if (special.Key == LogicalKey.None)
        {
            return null;
        }

        // Windows v1 can only express a functional key as the key being itself on its base layer.
        // Anything else is a per-layer remap the format has no room for.
        var isKeyBeingItself = layer == ModifierLayer.Default &&
                               special.Key == logicalKey &&
                               !WindowsLogicalKeyClassifier.ProducesCharacters(logicalKey);

        return isKeyBeingItself
            ? null
            : "Windows cannot build a functional key on this layer.";
    }

    private static string? DescribeXkb(KeyOutput output) =>
        output is SpecialKeyOutput { Key: LogicalKey.None }
            ? "Choose a key for this layer, or set it to No output."
            : null;
}
