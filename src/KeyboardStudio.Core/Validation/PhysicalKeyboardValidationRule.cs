namespace KeyboardStudio.Core;

public sealed class PhysicalKeyboardValidationRule : IKeyboardProjectValidationRule
{
    public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var issues = new List<ValidationIssue>();
        foreach (var group in project.Keyboard.Keys
                     .GroupBy(key => key.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.DuplicatePhysicalKeyId,
                $"Physical key id '{group.Key}' is duplicated.",
                group.Key));
        }

        foreach (var key in project.Keyboard.Keys.Where(key => key.ScanCode is < 0 or > 0xFF))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.InvalidScanCode,
                $"Scan code {key.ScanCode} is outside the supported byte range.",
                key.Id));
        }

        foreach (var group in project.Keyboard.Keys
                     .GroupBy(key => (key.ScanCode, key.Extended))
                     .Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.DuplicateScanCodeIdentity,
                $"Scan code 0x{group.Key.ScanCode:X2} is mapped by more than one physical key."));
        }

        return issues;
    }
}
