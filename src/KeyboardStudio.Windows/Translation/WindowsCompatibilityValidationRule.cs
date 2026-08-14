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
                mapping.Outputs.Values.OfType<SpecialKeyOutput>().Any(output => output.Key == LogicalKey.None))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    WindowsDiagnosticCodes.UnsupportedLogicalKeyMapping,
                    $"Physical key '{mapping.KeyId}' has output mappings without a supported logical key.",
                    mapping.KeyId));
            }

            foreach (var layer in mapping.Outputs.Keys.Where(layer => !IsSupportedLayer(layer)))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    WindowsDiagnosticCodes.UnsupportedModifierCombination,
                    $"Modifier layer value '{(int)layer}' is not supported by the Windows backend.",
                    mapping.KeyId));
            }
        }

        return issues;
    }

    private static bool IsSupportedLayer(ModifierLayer layer) =>
        layer is ModifierLayer.Default or
            ModifierLayer.Shift or
            ModifierLayer.AltGr or
            ModifierLayer.ShiftAltGr;
}
