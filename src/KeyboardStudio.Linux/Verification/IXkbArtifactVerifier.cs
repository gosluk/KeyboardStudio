namespace KeyboardStudio.Linux;

public interface IXkbArtifactVerifier
{
    Task<XkbVerificationResult> VerifyAsync(
        XkbKeyboardLayout layout,
        XkbGeneratedSymbols generated,
        string xkbRoot,
        bool requireExternalVerification,
        CancellationToken cancellationToken = default);
}
