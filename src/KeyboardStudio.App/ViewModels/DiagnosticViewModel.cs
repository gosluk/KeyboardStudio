using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class DiagnosticViewModel
{
    public DiagnosticViewModel(ValidationIssue issue, Action<string> selectKey)
        : this(
            (issue ?? throw new ArgumentNullException(nameof(issue))).Severity,
            issue.Code,
            issue.Message,
            issue.KeyId,
            selectKey)
    {
    }

    /// <summary>
    /// A finding from somewhere other than validation — a build, an installation — shown in the
    /// same row so that it behaves the same way: naming a key makes it a place to go, not just a
    /// string that mentions one.
    /// </summary>
    public DiagnosticViewModel(
        ValidationSeverity severity,
        string code,
        string message,
        string? keyId,
        Action<string> selectKey)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(selectKey);

        Severity = severity;
        Code = code;
        Message = message;
        KeyId = keyId;
        // Always executable: a Button bound to a command that cannot run is disabled, and a row
        // that names no key still has something to say. Doing nothing is the right no-op.
        SelectCommand = new RelayCommand(() =>
        {
            if (KeyId is not null)
            {
                selectKey(KeyId);
            }
        });
    }

    public ValidationSeverity Severity { get; }

    public string Code { get; }

    public string Message { get; }

    public string? KeyId { get; }

    /// <summary>Whether this finding points at a key the editor can show.</summary>
    public bool HasKey => KeyId is not null;

    public bool IsError => Severity == ValidationSeverity.Error;

    public string KeyAssociation => KeyId is null ? string.Empty : $"Key: {KeyId}";

    public IRelayCommand SelectCommand { get; }
}
