namespace KeyboardStudio.Linux;

public interface IXkbUserBundleVerifier
{
    Task<XkbUserBundleVerificationResult> VerifyAsync(
        string bundleRoot,
        IReadOnlyList<XkbUserVariantMetadata> variants,
        XkbUserInstallCapability capability,
        bool requireBundleManifest = true,
        CancellationToken cancellationToken = default);

    Task<XkbUserBundleVerificationResult> VerifyBaseAsync(
        string bundleRoot,
        XkbUserVariantMetadata removedVariant,
        XkbUserInstallCapability capability,
        CancellationToken cancellationToken = default);
}
