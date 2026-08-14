namespace KeyboardStudio.Linux;

public interface IXkbKeyNameMapper
{
    XkbKeyNameMappingResult Map(string templateId, string keyId);
}
