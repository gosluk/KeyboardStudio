using KeyboardStudio.Core;

namespace KeyboardStudio.App;

public interface IProjectDocumentService
{
    KeyboardProject? CurrentProject { get; }
    string? CurrentFilePath { get; }
    bool IsDirty { get; }
    ProjectDocumentError? LastError { get; }

    KeyboardProject CreateNew();
    void MarkDirty();
    Task<ProjectDocumentOperationResult> OpenAsync(string path, CancellationToken cancellationToken = default);
    Task<ProjectDocumentOperationResult> SaveAsync(CancellationToken cancellationToken = default);
    Task<ProjectDocumentOperationResult> SaveAsAsync(string path, CancellationToken cancellationToken = default);
}
