namespace KeyboardStudio.Core;

public sealed class MetadataValidationRule : IKeyboardProjectValidationRule
{
    public IReadOnlyList<ValidationIssue> Validate(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(project.Metadata.Name))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "META001",
                "Project display name must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(project.Metadata.Version))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "META002",
                "Project version must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(project.Metadata.Language))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "META003",
                "Project language or locale must not be empty."));
        }

        return issues;
    }
}
