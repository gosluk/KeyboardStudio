namespace KeyboardStudio.Core;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? KeyId = null);

public interface IKeyboardProjectValidator
{
    IReadOnlyList<ValidationIssue> Validate(KeyboardProject project);
}

public sealed class KeyboardProjectValidator : IKeyboardProjectValidator
{
    public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var issues = new List<ValidationIssue>();

        foreach (var group in project.Keyboard.Keys.GroupBy(key => key.Id, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "KEY001",
                $"Physical key id '{group.Key}' is duplicated.",
                group.Key));
        }

        foreach (var key in project.Keyboard.Keys.Where(key => key.ScanCode is < 0 or > 0xFF))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "KEY002",
                $"Scan code {key.ScanCode} is outside the supported byte range.",
                key.Id));
        }

        foreach (var group in project.Keyboard.Keys
                     .GroupBy(key => (key.ScanCode, key.Extended))
                     .Where(group => group.Count() > 1))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "KEY003",
                $"Scan code 0x{group.Key.ScanCode:X2} is mapped by more than one physical key."));
        }

        var physicalIds = project.Keyboard.Keys.Select(key => key.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var mapping in project.Layout.Mappings.Where(mapping => !physicalIds.Contains(mapping.KeyId)))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MAP001",
                $"Mapping references unknown physical key '{mapping.KeyId}'.",
                mapping.KeyId));
        }

        if (string.IsNullOrWhiteSpace(project.Metadata.Name))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "META001",
                "Project name must not be empty."));
        }

        return issues;
    }
}
