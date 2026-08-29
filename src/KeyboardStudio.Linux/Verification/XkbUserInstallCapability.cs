namespace KeyboardStudio.Linux;

public sealed record XkbUserInstallCapability(
    XkbUserInstallMode Mode,
    XkbSessionType SessionType,
    string? UserXkbRoot,
    string? StateRoot,
    bool PathsAreSafe,
    string? XkbCliPath,
    string? XkbCliVersionOutput,
    Version? LibXkbCommonVersion,
    bool MeetsRecommendedVersion,
    string? CanonicalSystemRoot,
    XkbRegistryDiscoverySupport RegistryDiscovery,
    IReadOnlyList<XkbDiagnostic> Diagnostics);
