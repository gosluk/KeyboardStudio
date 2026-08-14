namespace KeyboardStudio.Core;

public sealed class KeyboardTemplateException : Exception
{
    public KeyboardTemplateException(
        KeyboardTemplateErrorCode code,
        string templateId,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        TemplateId = templateId;
    }

    public KeyboardTemplateErrorCode Code { get; }

    public string TemplateId { get; }
}
