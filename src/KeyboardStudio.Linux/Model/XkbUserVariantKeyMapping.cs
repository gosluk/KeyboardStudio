namespace KeyboardStudio.Linux;

/// <param name="Keysyms">
/// Every level the generated key is written with, in level order. Longer than the four the model
/// holds whenever the imported source gave the key more and those levels are being kept.
/// </param>
/// <param name="SourceTypeName">
/// The key type to write instead of the one derived from <paramref name="Type"/>, used when only
/// the source's own type can reach the levels beyond the model's four.
/// </param>
public sealed record XkbUserVariantKeyMapping(
    string PhysicalKeyId,
    string KeyName,
    XkbKeyType Type,
    IReadOnlyList<string> Keysyms,
    string? SourceTypeName = null);
