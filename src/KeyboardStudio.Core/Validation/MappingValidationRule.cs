using System.Text;

namespace KeyboardStudio.Core;

public sealed class MappingValidationRule : IKeyboardProjectValidationRule
{
    public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var issues = new List<ValidationIssue>();
        var physicalIds = project.Keyboard.Keys
            .Select(key => key.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var mapping in project.Layout.Mappings.Where(mapping => !physicalIds.Contains(mapping.KeyId)))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.MappingReferencesMissingKey,
                $"Mapping references unknown physical key '{mapping.KeyId}'.",
                mapping.KeyId));
        }

        foreach (var mapping in project.Layout.Mappings)
        {
            if (mapping.LogicalKey == LogicalKey.None && mapping.Outputs.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    KeyboardProjectDiagnosticCodes.OutputWithoutLogicalKey,
                    $"Physical key '{mapping.KeyId}' has outputs but no logical-key assignment.",
                    mapping.KeyId));
            }

            foreach (var output in mapping.Outputs.Values)
            {
                if (output is null ||
                    output is CharacterOutput character && !IsSingleUnicodeScalar(character.Value))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        KeyboardProjectDiagnosticCodes.InvalidCharacterOutput,
                        $"Mapping for physical key '{mapping.KeyId}' contains an invalid character output.",
                        mapping.KeyId));
                }
            }
        }

        foreach (var group in project.Layout.Mappings
                     .GroupBy(mapping => mapping.KeyId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.DuplicateKeyMapping,
                $"Physical key '{group.Key}' has more than one mapping.",
                group.Key));
        }

        return issues;
    }

    private static bool IsSingleUnicodeScalar(string? value) =>
        !string.IsNullOrEmpty(value) &&
        Rune.TryGetRuneAt(value, 0, out var rune) &&
        rune.Utf16SequenceLength == value.Length;
}
