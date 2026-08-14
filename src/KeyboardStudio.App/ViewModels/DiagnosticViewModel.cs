using CommunityToolkit.Mvvm.Input;
using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public sealed class DiagnosticViewModel
{
    public DiagnosticViewModel(ValidationIssue issue, Action<string> selectKey)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(selectKey);

        Severity = issue.Severity;
        Code = issue.Code;
        Message = issue.Message;
        KeyId = issue.KeyId;
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

    public string KeyAssociation => KeyId is null ? string.Empty : $"Key: {KeyId}";

    public IRelayCommand SelectCommand { get; }
}
