using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public sealed class WindowsTranslationException : Exception
{
    public WindowsTranslationException()
        : this("The project could not be translated to a Windows keyboard layout.")
    {
    }

    public WindowsTranslationException(string? message)
        : base(message)
    {
    }

    public WindowsTranslationException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public WindowsTranslationException(IEnumerable<ValidationIssue> issues)
        : base("The project contains mappings that cannot be translated to a Windows keyboard layout.")
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues.ToArray();
    }

    public IReadOnlyList<ValidationIssue> Issues { get; } = [];
}
