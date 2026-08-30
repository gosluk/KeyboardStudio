namespace KeyboardStudio.Linux;

public sealed record XkbInstallOperation(
    XkbInstallOperationKind Kind,
    string RelativePath,
    string DestinationPath,
    string? ExpectedExistingSha256,
    string? Content,
    string? ContentSha256);
