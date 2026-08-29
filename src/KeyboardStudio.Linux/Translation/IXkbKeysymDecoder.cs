using KeyboardStudio.Core;

namespace KeyboardStudio.Linux;

/// <summary>
/// Turns an XKB keysym name into a model output — the import-side inverse of
/// <see cref="IXkbKeysymMapper"/>.
/// </summary>
public interface IXkbKeysymDecoder
{
    /// <summary>
    /// Decodes one keysym.
    /// </summary>
    /// <param name="keysym">The keysym name as the symbols file wrote it.</param>
    /// <param name="keyId">
    /// The physical key being decoded, used only to address any diagnostic so the editor can jump
    /// to it. Pass <see langword="null"/> when there is no key to point at.
    /// </param>
    /// <param name="layer">The layer being decoded, for the same reason.</param>
    XkbKeysymDecodeResult Decode(string keysym, string? keyId = null, ModifierLayer? layer = null);
}
