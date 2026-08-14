namespace KeyboardStudio.Core;

public enum KeyboardTemplateErrorCode
{
    UnknownTemplate,
    ResourceUnavailable,
    InvalidJson,
    UnsupportedSchemaVersion,
    TemplateIdentityMismatch,
    InvalidTemplateMetadata,
    InvalidKeyDefinition,
    DuplicateKeyId,
    DuplicateScanCodeIdentity,
    IncompleteTemplate
}
