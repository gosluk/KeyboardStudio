namespace KeyboardStudio.Core;

public interface IKeyboardTemplateContentSource
{
    Stream OpenRead(string templateId);
}
