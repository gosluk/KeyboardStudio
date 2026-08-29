namespace KeyboardStudio.Linux;

/// <summary>
/// Names the XKB key that a template's physical key is written as.
/// </summary>
public interface IXkbKeyNameMapper
{
    /// <summary>
    /// Maps one physical key of one template to its XKB key name.
    /// </summary>
    XkbKeyNameMappingResult Map(string templateId, string keyId);

    /// <summary>
    /// Returns the whole table for a template, keyed by physical key identity.
    ///
    /// Exposed so that import can invert the same data generation writes, rather than keep a second
    /// table of its own. Two tables would eventually disagree — <c>&lt;LSGT&gt;</c> exists on
    /// <c>iso-105</c> and not on <c>ansi-104</c>, and the key that carries <c>&lt;BKSL&gt;</c>
    /// differs between them — and a layout that survived a round trip through the wrong half would
    /// come back with keys silently moved.
    /// </summary>
    /// <param name="templateId">The template to describe.</param>
    /// <returns>
    /// Physical key identity to XKB key name. Empty for a template that has no table, which is the
    /// same answer <see cref="Map"/> gives by failing for every key of it.
    /// </returns>
    IReadOnlyDictionary<string, string> GetMappings(string templateId);
}
