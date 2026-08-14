namespace KeyboardStudio.Core;

public sealed class ValidationResult
{
    public ValidationResult(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues.ToArray();
        if (Issues.Any(issue => issue is null))
        {
            throw new ArgumentException("Validation issues must not contain null entries.", nameof(issues));
        }
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool HasErrors => Issues.Any(issue => issue.Severity == ValidationSeverity.Error);

    public bool HasWarnings => Issues.Any(issue => issue.Severity == ValidationSeverity.Warning);

    public bool HasInformation => Issues.Any(issue => issue.Severity == ValidationSeverity.Info);

    public bool IsValid => !HasErrors;
}
