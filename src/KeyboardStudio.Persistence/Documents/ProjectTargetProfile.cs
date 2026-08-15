namespace KeyboardStudio.Persistence;

public sealed record ProjectTargetProfile(
    string Target,
    IReadOnlyDictionary<string, string> Settings);
