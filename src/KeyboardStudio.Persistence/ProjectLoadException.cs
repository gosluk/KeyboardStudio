namespace KeyboardStudio.Persistence;

public sealed class ProjectLoadException : Exception
{
    public ProjectLoadException()
        : this(ProjectLoadErrorCode.Unknown, "The project could not be loaded.")
    {
    }

    public ProjectLoadException(string? message)
        : base(message)
    {
    }

    public ProjectLoadException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public ProjectLoadException(
        ProjectLoadErrorCode errorCode,
        string message,
        int? schemaVersion = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        SchemaVersion = schemaVersion;
    }

    public ProjectLoadErrorCode ErrorCode { get; } = ProjectLoadErrorCode.Unknown;

    public int? SchemaVersion { get; }
}
