namespace KeyboardStudio.Core;

public sealed class EmbeddedKeyboardTemplateContentSource : IKeyboardTemplateContentSource
{
    private const string ResourcePrefix = "KeyboardStudio.Core.KeyboardTemplates.";

    public Stream OpenRead(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("A template ID is required.", nameof(templateId));
        }

        var resourceName = $"{ResourcePrefix}{templateId}.json";
        return typeof(EmbeddedKeyboardTemplateContentSource).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded keyboard template resource '{resourceName}' was not found.",
                resourceName);
    }
}
