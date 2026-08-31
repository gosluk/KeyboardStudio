namespace KeyboardStudio.Linux;

/// <summary>
/// Flattens a symbols section and everything it is composed from into one set of keys.
/// </summary>
public interface IXkbSymbolsResolver
{
    /// <summary>
    /// Resolves one section on its own, or <see langword="null"/> when no root holds the file or the
    /// file has no such section. Resolution never throws: anything it cannot use comes back as a
    /// diagnostic on the result.
    /// </summary>
    /// <param name="file">Symbols file name relative to a <c>symbols/</c> directory.</param>
    /// <param name="section">Section name, or <see langword="null"/> for the file's default section.</param>
    ResolvedXkbSymbols? Resolve(string file, string? section);

    /// <summary>
    /// Resolves one section as a whole keyboard, composing it onto <see cref="XkbCommonBase"/> the
    /// way <c>rules/evdev</c> does. This is what an import wants; <see cref="Resolve"/> is what a
    /// caller wants when it is asking about the file itself.
    /// </summary>
    /// <param name="file">Symbols file name relative to a <c>symbols/</c> directory.</param>
    /// <param name="section">Section name, or <see langword="null"/> for the file's default section.</param>
    ResolvedXkbSymbols? ResolveLayout(string file, string? section);
}
