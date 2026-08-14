namespace KeyboardStudio.Core;

public interface IKeyboardTemplateProvider
{
    IReadOnlyList<KeyboardTemplateDescriptor> Templates { get; }

    PhysicalKeyboard Load(string templateId);
}
