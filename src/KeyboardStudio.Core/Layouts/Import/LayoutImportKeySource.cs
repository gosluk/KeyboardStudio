namespace KeyboardStudio.Core;

/// <summary>
/// What the source said about one physical key, verbatim, including the parts the model cannot
/// hold.
///
/// The model keeps four modifier layers of representable characters, and that is the right shape
/// for an editor. It is not the whole truth about a key: a fifth level, a dead key, a keysym with
/// no character behind it. Import used to drop those and mark the key unsafe to override, because
/// an override rewrites a key completely and would have erased them.
///
/// So the source's own words are kept beside the projection. They are opaque here on purpose —
/// <see cref="Levels"/> and <see cref="KeyType"/> mean nothing to the model and are never shown or
/// edited. They exist so that the backend that read them can write back the parts of a key it was
/// never able to represent, and the user's change can be applied to the rest.
/// </summary>
/// <param name="KeyId">The physical key, in editor identity.</param>
/// <param name="SourceKeyName">The key's name in the source format, kept for diagnostics.</param>
/// <param name="Levels">
/// Every level the source defined, in level order and in the source's own notation. Longer than
/// the model's four layers whenever the key has more.
/// </param>
/// <param name="KeyType">
/// The key type the source declared, or <see langword="null"/> when it declared none and left the
/// format to infer one. It is what makes levels beyond the model's four reachable at all.
/// </param>
public sealed record LayoutImportKeySource(
    string KeyId,
    string SourceKeyName,
    IReadOnlyList<string> Levels,
    string? KeyType);
