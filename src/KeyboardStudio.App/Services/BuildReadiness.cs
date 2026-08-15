using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed record BuildReadiness(
    BuildEnvironmentStatus Environment,
    IReadOnlyList<ValidationIssue> CommonIssues,
    IReadOnlyList<ValidationIssue> TargetIssues)
{
    public bool HasCommonErrors =>
        CommonIssues.Any(issue => issue.Severity == ValidationSeverity.Error);

    public bool HasTargetErrors =>
        TargetIssues.Any(issue => issue.Severity == ValidationSeverity.Error);

    public bool CanBuild => Environment.Available && !HasCommonErrors && !HasTargetErrors;
}
