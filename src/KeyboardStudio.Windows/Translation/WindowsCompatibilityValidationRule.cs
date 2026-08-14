using System.Text;
using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public sealed class WindowsCompatibilityValidationRule : IKeyboardProjectValidationRule
{
    public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var issues = new List<ValidationIssue>();
        foreach (var mapping in project.Layout.Mappings)
        {
            if ((mapping.LogicalKey == LogicalKey.None && mapping.Outputs.Count > 0) ||
                mapping.Outputs.Values.OfType<SpecialKeyOutput>().Any(output => output.Key == LogicalKey.None) ||
                (mapping.LogicalKey != LogicalKey.None &&
                 !WindowsVirtualKeyMapper.TryMap(mapping.LogicalKey, out _)))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    WindowsDiagnosticCodes.UnsupportedLogicalKeyMapping,
                    $"Physical key '{mapping.KeyId}' has output mappings without a supported logical key.",
                    mapping.KeyId));
            }

            foreach (var output in mapping.Outputs)
            {
                if (!IsSupportedLayer(output.Key))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        WindowsDiagnosticCodes.UnsupportedModifierCombination,
                        $"Modifier layer value '{(int)output.Key}' is not supported by the Windows backend.",
                        mapping.KeyId));
                }

                switch (output.Value)
                {
                    case CharacterOutput characterOutput:
                        ValidateCharacterOutput(mapping, characterOutput, issues);
                        break;
                    case SpecialKeyOutput specialKeyOutput:
                        ValidateSpecialKeyOutput(mapping, output.Key, specialKeyOutput, issues);
                        break;
                    case NoOutput:
                        break;
                    case null:
                        AddUnsupportedCharacterIssue(
                            mapping,
                            "contains a null output that cannot be translated",
                            issues);
                        break;
                    default:
                        AddUnsupportedCharacterIssue(
                            mapping,
                            $"uses unsupported output type '{output.Value.GetType().Name}'",
                            issues);
                        break;
                }
            }
        }

        return issues;
    }

    private static bool IsSupportedLayer(ModifierLayer layer) =>
        layer is ModifierLayer.Default or
            ModifierLayer.Shift or
            ModifierLayer.AltGr or
            ModifierLayer.ShiftAltGr;

    private static void ValidateCharacterOutput(
        KeyMapping mapping,
        CharacterOutput output,
        List<ValidationIssue> issues)
    {
        if (mapping.LogicalKey == LogicalKey.None)
        {
            return;
        }

        if (!WindowsLogicalKeyClassifier.ProducesCharacters(mapping.LogicalKey))
        {
            AddUnsupportedCharacterIssue(
                mapping,
                $"assigns a character output to scan-only logical key '{mapping.LogicalKey}'",
                issues);
            return;
        }

        var rune = output.Value.EnumerateRunes().First();
        if (!rune.IsBmp)
        {
            AddUnsupportedCharacterIssue(
                mapping,
                $"uses non-BMP character U+{rune.Value:X} which requires ligature support",
                issues);
        }
    }

    private static void ValidateSpecialKeyOutput(
        KeyMapping mapping,
        ModifierLayer layer,
        SpecialKeyOutput output,
        List<ValidationIssue> issues)
    {
        if (output.Key == LogicalKey.None)
        {
            return;
        }

        if (layer == ModifierLayer.Default &&
            output.Key == mapping.LogicalKey &&
            !WindowsLogicalKeyClassifier.ProducesCharacters(mapping.LogicalKey))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            WindowsDiagnosticCodes.UnsupportedSpecialKeyMapping,
            $"Physical key '{mapping.KeyId}' uses a layer-specific special-key mapping that Windows v1 cannot represent.",
            mapping.KeyId));
    }

    private static void AddUnsupportedCharacterIssue(
        KeyMapping mapping,
        string reason,
        List<ValidationIssue> issues) =>
        issues.Add(new ValidationIssue(
            ValidationSeverity.Error,
            WindowsDiagnosticCodes.UnsupportedCharacterMapping,
            $"Physical key '{mapping.KeyId}' {reason}.",
            mapping.KeyId));
}
