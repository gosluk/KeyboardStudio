using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class ProjectDocumentServiceTests
{
    [Fact]
    public void New_WhenInvoked_CreatesCleanUntitledDocument()
    {
        var service = CreateService();

        var project = service.New();

        Assert.Same(project, service.CurrentProject);
        Assert.Null(service.CurrentFilePath);
        Assert.False(service.IsDirty);
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task SaveAsync_WhenDocumentHasNoPath_RequiresSaveAs()
    {
        var service = CreateService();
        service.New();
        service.MarkDirty();

        var result = await service.SaveAsync();

        Assert.False(result.Success);
        Assert.Equal(ProjectDocumentErrorKind.SaveAsRequired, result.Error?.Kind);
        Assert.True(service.IsDirty);
        Assert.Null(service.CurrentFilePath);
    }

    [Fact]
    public async Task SaveAsAsync_WhenDocumentIsDirty_SavesAndClearsDirtyState()
    {
        var path = CreateTemporaryPath();
        try
        {
            var service = CreateService();
            var project = service.New();
            service.MarkDirty();

            var result = await service.SaveAsAsync(path);

            Assert.True(result.Success);
            Assert.Equal(Path.GetFullPath(path), service.CurrentFilePath);
            Assert.False(service.IsDirty);
            Assert.Null(service.LastError);

            await using var stream = File.OpenRead(path);
            var loaded = await new JsonKeyboardProjectStore().LoadAsync(stream);
            Assert.Equal(project.Metadata.Name, loaded.Metadata.Name);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task OpenAsync_WhenProjectIsValid_LoadsDocumentAndTracksPath()
    {
        var path = CreateTemporaryPath();
        try
        {
            var source = DemoProjectFactory.Create();
            await using (var stream = File.Create(path))
            {
                await new JsonKeyboardProjectStore().SaveAsync(source, stream);
            }

            var service = CreateService();
            service.New();
            service.MarkDirty();

            var result = await service.OpenAsync(path);

            Assert.True(result.Success);
            Assert.NotNull(service.CurrentProject);
            Assert.Equal(source.Metadata.Name, service.CurrentProject.Metadata.Name);
            Assert.Equal(Path.GetFullPath(path), service.CurrentFilePath);
            Assert.False(service.IsDirty);
            Assert.Null(service.LastError);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task OpenAsync_WhenProjectIsInvalid_ReportsPresentationError()
    {
        var path = CreateTemporaryPath();
        try
        {
            await File.WriteAllTextAsync(path, "{\"schemaVersion\":1}");
            var service = CreateService();

            var result = await service.OpenAsync(path);

            Assert.False(result.Success);
            Assert.Equal(ProjectDocumentErrorKind.InvalidProject, result.Error?.Kind);
            Assert.Equal(ProjectDocumentErrorKind.InvalidProject, service.LastError?.Kind);
            Assert.Null(service.CurrentProject);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task SaveAsAsync_WhenPersistenceFails_PreservesDirtyStateAndCurrentPath()
    {
        var firstPath = CreateTemporaryPath();
        var secondPath = CreateTemporaryPath();
        try
        {
            var store = new ControllableProjectStore();
            var service = new ProjectDocumentService(store, DemoProjectFactory.Create);
            service.New();

            var firstResult = await service.SaveAsAsync(firstPath);
            Assert.True(firstResult.Success);
            var originalPath = service.CurrentFilePath;

            service.MarkDirty();
            store.FailOnSave = true;

            var secondResult = await service.SaveAsAsync(secondPath);

            Assert.False(secondResult.Success);
            Assert.Equal(ProjectDocumentErrorKind.Io, secondResult.Error?.Kind);
            Assert.True(service.IsDirty);
            Assert.Equal(originalPath, service.CurrentFilePath);
        }
        finally
        {
            DeleteIfExists(firstPath);
            DeleteIfExists(secondPath);
        }
    }

    private static ProjectDocumentService CreateService() =>
        new(new JsonKeyboardProjectStore(), DemoProjectFactory.Create);

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}.kbdproj");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class ControllableProjectStore : IKeyboardProjectStore
    {
        public bool FailOnSave { get; set; }

        public Task SaveAsync(
            KeyboardProject project,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if (FailOnSave)
            {
                throw new IOException("Simulated save failure.");
            }

            return Task.CompletedTask;
        }

        public Task<KeyboardProject> LoadAsync(
            Stream source,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DemoProjectFactory.Create());
    }
}
