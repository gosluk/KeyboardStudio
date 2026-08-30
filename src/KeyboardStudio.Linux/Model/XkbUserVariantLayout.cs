namespace KeyboardStudio.Linux;

public sealed record XkbUserVariantLayout(
    XkbUserVariantMetadata Metadata,
    IReadOnlyList<XkbUserVariantKeyMapping> Mappings,
    bool UsesLevelThree);
