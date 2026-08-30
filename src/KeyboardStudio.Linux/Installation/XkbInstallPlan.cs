namespace KeyboardStudio.Linux;

public sealed record XkbInstallPlan(
    XkbInstallAction Action,
    string ProjectInstallationId,
    IReadOnlyList<XkbInstallOperation> Operations,
    XkbInstallationManifest UpdatedManifest,
    string ManifestPath);
