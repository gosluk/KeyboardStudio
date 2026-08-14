using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

public sealed record KeyboardProjectDocument(
    KeyboardProject Project,
    IReadOnlyDictionary<string, ProjectTargetProfile> TargetProfiles);
