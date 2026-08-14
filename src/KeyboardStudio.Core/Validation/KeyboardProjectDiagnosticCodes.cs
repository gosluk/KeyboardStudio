namespace KeyboardStudio.Core;

public static class KeyboardProjectDiagnosticCodes
{
    public const string DuplicatePhysicalKeyId = "KSP001";
    public const string InvalidScanCode = "KSP002";
    public const string DuplicateScanCodeIdentity = "KSP003";
    public const string MissingProjectName = "KSP101";
    public const string MissingProjectVersion = "KSP102";
    public const string MissingProjectLanguage = "KSP103";
    public const string MissingProjectDescription = "KSP104";
    public const string MappingReferencesMissingKey = "KSM001";
    public const string InvalidCharacterOutput = "KSM002";
    public const string DuplicateKeyMapping = "KSM003";
    public const string OutputWithoutLogicalKey = "KSM100";
}
