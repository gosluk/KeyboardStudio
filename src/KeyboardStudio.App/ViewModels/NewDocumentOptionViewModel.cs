using CommunityToolkit.Mvvm.Input;

namespace KeyboardStudio.App;

/// <summary>
/// One explicit new-document choice in the File menu.
/// </summary>
/// <remarks>
/// Each option carries its own command, so the menu that presents them needs no binding back to
/// the window that owns them.
/// </remarks>
public sealed class NewDocumentOptionViewModel
{
    public NewDocumentOptionViewModel(string templateId, string name, IAsyncRelayCommand command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(command);

        TemplateId = templateId;
        Name = name;
        Command = command;
    }

    public string TemplateId { get; }

    /// <summary>The menu label, which is also the accessible name of the choice.</summary>
    public string Name { get; }

    public IAsyncRelayCommand Command { get; }
}
