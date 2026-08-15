using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

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

    public ProjectDocumentError? LastError { get; private set; }

    public KeyboardProject CreateNew()
    {
        var document = _documentFactory();
        CurrentProject = document.Project;
        CurrentTargetProfiles = CopyProfiles(document.TargetProfiles);
        CurrentFilePath = null;
        IsDirty = false;
        LastError = null;
        return document.Project;
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
            CurrentFilePath = fullPath;
            IsDirty = false;
            LastError = null;
            return ProjectDocumentOperationResult.Succeeded();
        }
        catch (ProjectLoadException exception)
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
            var document = new KeyboardProjectDocument(CurrentProject!, CurrentTargetProfiles);
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
