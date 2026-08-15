namespace KeyboardStudio.App;

public enum BuildProblemKind
{
    ProjectValidation,
    TargetCompatibility,
    SourceGeneration,
    MissingRequiredToolchain,
    OptionalVerifierUnavailable,
    CompilerOrLinker,
    ArtifactVerification
}
