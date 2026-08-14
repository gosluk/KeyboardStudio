namespace KeyboardStudio.App;

public readonly record struct ProjectDocumentOperationResult(bool Success, ProjectDocumentError? Error)
{
    public static ProjectDocumentOperationResult Succeeded() => new(true, null);

    public static ProjectDocumentOperationResult Failed(ProjectDocumentError error) => new(false, error);
}
