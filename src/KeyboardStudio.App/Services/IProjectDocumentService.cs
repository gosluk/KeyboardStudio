using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

public interface IProjectDocumentService
{
    KeyboardProject? CurrentProject { get; }
    IReadOnlyDictionary<string, ProjectTargetProfile> CurrentTargetProfiles { get; }
    string? CurrentFilePath { get; }
    bool IsDirty { get; }
    LayoutImportProvenance? CurrentProvenance { get; }
    ProjectDocumentError? LastError { get; }

    KeyboardProject CreateNew();
    KeyboardProject Adopt(KeyboardProjectDocument document);
    void RecordProvenance(LayoutImportProvenance? provenance);
    void MarkDirty();
    void UpdateTargetProfiles(IReadOnlyDictionary<string, ProjectTargetProfile> targetProfiles);
    Task<ProjectDocumentOperationResult> OpenAsync(string path, CancellationToken cancellationToken = default);
    Task<ProjectDocumentOperationResult> SaveAsync(CancellationToken cancellationToken = default);
    Task<ProjectDocumentOperationResult> SaveAsAsync(string path, CancellationToken cancellationToken = default);
}
