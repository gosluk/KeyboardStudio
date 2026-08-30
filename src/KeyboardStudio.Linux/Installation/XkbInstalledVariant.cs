namespace KeyboardStudio.Linux;

public sealed record XkbInstalledVariant(
    string ProjectInstallationId,
    string BaseLayoutId,
    string? BaseVariantId,
    string ResolvedBaseSectionId,
    string PublicVariantId,
    string InternalSectionId,
    string Description,
    string CentralBlockSha256,
    string BridgeBlockSha256,
    string RegistryEntrySha256,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset VerifiedAtUtc,
    string? ToolVersion);
