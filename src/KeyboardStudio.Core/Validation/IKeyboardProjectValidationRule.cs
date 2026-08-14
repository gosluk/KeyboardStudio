namespace KeyboardStudio.Core;

public interface IKeyboardProjectValidationRule
{
    IReadOnlyList<ValidationIssue> Validate(KeyboardProject project);
}
