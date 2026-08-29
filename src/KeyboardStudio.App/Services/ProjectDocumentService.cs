using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using System.Text.Json;

namespace KeyboardStudio.App;

public sealed class ProjectDocumentService : IProjectDocumentService
{
    private readonly IKeyboardProjectDocumentStore _store;
    private readonly Func<KeyboardProjectDocument> _documentFactory;

    public ProjectDocumentService(
        IKeyboardProjectDocumentStore store,
        Func<KeyboardProjectDocument> documentFactory)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(documentFactory);

        _store = store;
        _documentFactory = documentFactory;
    }

    public KeyboardProject? CurrentProject { get; private set; }

    public IReadOnlyDictionary<string, ProjectTargetProfile> CurrentTargetProfiles { get; private set; } =
        new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal);

    public string? CurrentFilePath { get; private set; }

    public bool IsDirty { get; private set; }

    /// <summary>
    /// Where the open document was imported from, or null for one that was authored. It is carried
    /// here rather than on the project because it belongs to the saved document, and so has to
    /// survive every save without the editor having to remember to pass it along.
    /// </summary>
    public LayoutImportProvenance? CurrentProvenance { get; private set; }

    /// <summary>The immutable system-import baseline, when this document has one.</summary>
    public LayoutDerivation? CurrentLayoutDerivation { get; private set; }

    public ProjectDocumentError? LastError { get; private set; }

    public KeyboardProject CreateNew() => Adopt(_documentFactory());

    /// <summary>
    /// Takes a document that was produced rather than opened — a new one, or one just imported —
    /// as the current document.
    /// </summary>
    /// <remarks>
    /// It has no path and is not dirty, exactly like a new document: nothing has been written yet,
    /// and nothing has been changed since it was made. An import the user then abandons is no more
    /// a loss than a new document they abandon.
    /// </remarks>
    public KeyboardProject Adopt(KeyboardProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        CurrentProject = document.Project;
        CurrentTargetProfiles = CopyProfiles(document.TargetProfiles);
        CurrentProvenance = document.ImportProvenance;
        CurrentLayoutDerivation = document.LayoutDerivation;
        CurrentFilePath = null;
        IsDirty = false;
        LastError = null;
        return document.Project;
    }

    /// <summary>
    /// Records where the open document's mappings came from, keeping its path and its build
    /// settings. This is the other half of <see cref="Adopt"/>: an import laid onto a document the
    /// user is already working in changes where the layout came from without making a new document.
    /// </summary>
    public void RecordProvenance(LayoutImportProvenance? provenance)
    {
        if (CurrentProject is null)
        {
            return;
        }

        CurrentProvenance = provenance;
        CurrentLayoutDerivation = null;
        MarkDirty();
    }

    public void MarkDirty()
    {
        if (CurrentProject is not null)
        {
            IsDirty = true;
        }
    }

    public void UpdateTargetProfiles(IReadOnlyDictionary<string, ProjectTargetProfile> targetProfiles)
    {
        ArgumentNullException.ThrowIfNull(targetProfiles);
        CurrentTargetProfiles = CopyProfiles(targetProfiles);
        MarkDirty();
    }

    public async Task<ProjectDocumentOperationResult> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePath(path, out var fullPath, out var pathError))
        {
            return Fail(ProjectDocumentErrorKind.InvalidPath, pathError);
        }

        try
        {
            await using var stream = File.OpenRead(fullPath);
            var document = await _store.LoadAsync(stream, cancellationToken);

            CurrentProject = document.Project;
            CurrentTargetProfiles = CopyProfiles(document.TargetProfiles);
            CurrentProvenance = document.ImportProvenance;
            CurrentLayoutDerivation = document.LayoutDerivation;
            CurrentFilePath = fullPath;
            IsDirty = false;
            LastError = null;
            return ProjectDocumentOperationResult.Succeeded();
        }
        catch (ProjectLoadException exception)
        {
            return Fail(ProjectDocumentErrorKind.InvalidProject, exception.Message);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return Fail(ProjectDocumentErrorKind.InvalidProject, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail(ProjectDocumentErrorKind.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail(ProjectDocumentErrorKind.Io, exception.Message);
        }
    }

    public Task<ProjectDocumentOperationResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return Task.FromResult(Fail(
                ProjectDocumentErrorKind.NoProject,
                "There is no project to save."));
        }

        if (CurrentFilePath is null)
        {
            return Task.FromResult(Fail(
                ProjectDocumentErrorKind.SaveAsRequired,
                "The project does not have a file path yet. Use Save As first."));
        }

        return SaveToPathAsync(CurrentFilePath, updateCurrentPath: false, cancellationToken);
    }

    public Task<ProjectDocumentOperationResult> SaveAsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (CurrentProject is null)
        {
            return Task.FromResult(Fail(
                ProjectDocumentErrorKind.NoProject,
                "There is no project to save."));
        }

        if (!TryNormalizePath(path, out var fullPath, out var pathError))
        {
            return Task.FromResult(Fail(ProjectDocumentErrorKind.InvalidPath, pathError));
        }

        return SaveToPathAsync(fullPath, updateCurrentPath: true, cancellationToken);
    }

    private async Task<ProjectDocumentOperationResult> SaveToPathAsync(
        string path,
        bool updateCurrentPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.Create(path);
            var document = new KeyboardProjectDocument(
                CurrentProject!,
                CurrentTargetProfiles,
                CurrentProvenance,
                CurrentLayoutDerivation);
            await _store.SaveAsync(document, stream, cancellationToken);

            if (updateCurrentPath)
            {
                CurrentFilePath = path;
            }

            IsDirty = false;
            LastError = null;
            return ProjectDocumentOperationResult.Succeeded();
        }
        catch (UnauthorizedAccessException exception)
        {
            return Fail(ProjectDocumentErrorKind.AccessDenied, exception.Message);
        }
        catch (IOException exception)
        {
            return Fail(ProjectDocumentErrorKind.Io, exception.Message);
        }
    }

    private static Dictionary<string, ProjectTargetProfile> CopyProfiles(
        IReadOnlyDictionary<string, ProjectTargetProfile> profiles) =>
        profiles.ToDictionary(
            pair => pair.Key,
            pair => new ProjectTargetProfile(
                pair.Value.Target,
                new Dictionary<string, string>(pair.Value.Settings, StringComparer.Ordinal)),
            StringComparer.Ordinal);

    private ProjectDocumentOperationResult Fail(ProjectDocumentErrorKind kind, string message)
    {
        var error = new ProjectDocumentError(kind, message);
        LastError = error;
        return ProjectDocumentOperationResult.Failed(error);
    }

    private static bool TryNormalizePath(string path, out string fullPath, out string error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            fullPath = string.Empty;
            error = "A project file path is required.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(path);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            fullPath = string.Empty;
            error = exception.Message;
            return false;
        }
        catch (NotSupportedException exception)
        {
            fullPath = string.Empty;
            error = exception.Message;
            return false;
        }
        catch (PathTooLongException exception)
        {
            fullPath = string.Empty;
            error = exception.Message;
            return false;
        }
    }
}
