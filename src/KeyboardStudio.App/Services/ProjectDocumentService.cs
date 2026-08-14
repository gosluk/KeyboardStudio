using KeyboardStudio.Core;
using KeyboardStudio.Persistence;

namespace KeyboardStudio.App;

public enum ProjectDocumentErrorKind
{
    InvalidPath,
    NoProject,
    SaveAsRequired,
    InvalidProject,
    AccessDenied,
    Io
}

public sealed record ProjectDocumentError(ProjectDocumentErrorKind Kind, string Message);

public readonly record struct ProjectDocumentOperationResult(bool Success, ProjectDocumentError? Error)
{
    public static ProjectDocumentOperationResult Succeeded() => new(true, null);

    public static ProjectDocumentOperationResult Failed(ProjectDocumentError error) => new(false, error);
}

public interface IProjectDocumentService
{
    KeyboardProject? CurrentProject { get; }
    string? CurrentFilePath { get; }
    bool IsDirty { get; }
    ProjectDocumentError? LastError { get; }

    KeyboardProject New();
    void MarkDirty();
    Task<ProjectDocumentOperationResult> OpenAsync(string path, CancellationToken cancellationToken = default);
    Task<ProjectDocumentOperationResult> SaveAsync(CancellationToken cancellationToken = default);
    Task<ProjectDocumentOperationResult> SaveAsAsync(string path, CancellationToken cancellationToken = default);
}

public sealed class ProjectDocumentService : IProjectDocumentService
{
    private readonly IKeyboardProjectStore _store;
    private readonly Func<KeyboardProject> _projectFactory;

    public ProjectDocumentService(IKeyboardProjectStore store, Func<KeyboardProject> projectFactory)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(projectFactory);

        _store = store;
        _projectFactory = projectFactory;
    }

    public KeyboardProject? CurrentProject { get; private set; }

    public string? CurrentFilePath { get; private set; }

    public bool IsDirty { get; private set; }

    public ProjectDocumentError? LastError { get; private set; }

    public KeyboardProject New()
    {
        var project = _projectFactory();
        CurrentProject = project;
        CurrentFilePath = null;
        IsDirty = false;
        LastError = null;
        return project;
    }

    public void MarkDirty()
    {
        if (CurrentProject is not null)
        {
            IsDirty = true;
        }
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
            var project = await _store.LoadAsync(stream, cancellationToken);

            CurrentProject = project;
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

        return SaveToPathAsync(CurrentProject, CurrentFilePath, updateCurrentPath: false, cancellationToken);
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

        return SaveToPathAsync(CurrentProject, fullPath, updateCurrentPath: true, cancellationToken);
    }

    private async Task<ProjectDocumentOperationResult> SaveToPathAsync(
        KeyboardProject project,
        string path,
        bool updateCurrentPath,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.Create(path);
            await _store.SaveAsync(project, stream, cancellationToken);

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

    private ProjectDocumentOperationResult Fail(ProjectDocumentErrorKind kind, string message)
    {
        LastError = new ProjectDocumentError(kind, message);
        return ProjectDocumentOperationResult.Failed(LastError);
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
