namespace KeyboardStudio.Linux;

/// <summary>
/// Turns an XKB key name into a physical key of a template — the import-side inverse of
/// <see cref="IXkbKeyNameMapper"/>.
/// </summary>
public interface IXkbKeyNameResolver
{
    /// <summary>
    /// Resolves one key name, as a symbols file wrote it, including its angle brackets.
    /// </summary>
    /// <param name="templateId">The template the layout is being imported onto.</param>
    /// <param name="keyName">The XKB key name, such as <c>&lt;AE01&gt;</c>.</param>
    XkbKeyNameResolveResult Resolve(string templateId, string keyName);
}
