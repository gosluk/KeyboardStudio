namespace KeyboardStudio.Core;

public interface IKeyboardProjectValidator
{
    ValidationResult Validate(KeyboardProject project);
}
