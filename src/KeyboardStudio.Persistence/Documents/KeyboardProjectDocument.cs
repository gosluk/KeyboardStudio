using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

/// <summary>
/// One saved <c>.kbdproj</c>: the platform-neutral project, the per-target build settings the
/// application adds to it, and — for a document that began as an import — where it came from and
/// the immutable baseline from which a safe user variant can be derived.
/// </summary>
/// <param name="Project">The platform-neutral project.</param>
/// <param name="TargetProfiles">Build settings per target discriminator.</param>
/// <param name="ImportProvenance">
/// Where the project was imported from, or <see langword="null"/> for one that was authored rather
/// than imported. Optional because most documents have no import behind them, which is also why
/// adding it did not break the documents already written without it.
/// </param>
/// <param name="LayoutDerivation">
/// The system-import baseline used to calculate a derived user variant, or <see langword="null"/>
/// when the project was authored, imported from a loose file, or predates derivation capture.
/// </param>
public sealed record KeyboardProjectDocument(
    KeyboardProject Project,
    IReadOnlyDictionary<string, ProjectTargetProfile> TargetProfiles,
    LayoutImportProvenance? ImportProvenance = null,
    LayoutDerivation? LayoutDerivation = null);
