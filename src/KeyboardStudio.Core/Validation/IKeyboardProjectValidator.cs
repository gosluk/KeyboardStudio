namespace KeyboardStudio.Core;

public interface IKeyboardProjectValidator
{
    IReadOnlyList<ValidationIssue> Validate(KeyboardProject project);
}
