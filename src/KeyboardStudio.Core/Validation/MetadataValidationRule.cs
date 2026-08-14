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
                KeyboardProjectDiagnosticCodes.MissingProjectName,
                "Project display name must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(project.Metadata.Version))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.MissingProjectVersion,
                "Project version must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(project.Metadata.Language))
        {
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                KeyboardProjectDiagnosticCodes.MissingProjectLanguage,
                "Project language or locale must not be empty."));
        }

        return issues;
    }
}
