namespace KeyboardStudio.Persistence;

public enum ProjectLoadErrorCode
{
    Unknown,
    InvalidJson,
    MissingSchemaVersion,
    InvalidSchemaVersion,
    LegacySchemaRequiresMigration,
    UnsupportedFutureSchema,
    InvalidProject
}
