using KeyboardStudio.App;
using KeyboardStudio.Core;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.App.Tests;

public sealed class ProjectDocumentServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CreateNew_WhenInvoked_CreatesCleanUntitledDocument()
    {
        var service = CreateService();

        var project = service.CreateNew();

        Assert.Same(project, service.CurrentProject);
        Assert.Null(service.CurrentFilePath);
        Assert.False(service.IsDirty);
        Assert.Null(service.LastError);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAsync_WhenDocumentHasNoPath_RequiresSaveAs()
    {
        var service = CreateService();
        service.CreateNew();
        service.MarkDirty();

        var result = await service.SaveAsync();

        Assert.False(result.Success);
        Assert.Equal(ProjectDocumentErrorKind.SaveAsRequired, result.Error?.Kind);
        Assert.True(service.IsDirty);
        Assert.Null(service.CurrentFilePath);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveAsAsync_WhenDocumentIsDirty_SavesAndClearsDirtyState()
    {
        var path = CreateTemporaryPath();
        try
        {
            var service = CreateService();
            var project = service.CreateNew();
            service.MarkDirty();

            var result = await service.SaveAsAsync(path);

            Assert.True(result.Success);
            Assert.Equal(Path.GetFullPath(path), service.CurrentFilePath);
            Assert.False(service.IsDirty);
            Assert.Null(service.LastError);

            await using var stream = File.OpenRead(path);
            var loaded = await new JsonKeyboardProjectDocumentStore().LoadAsync(stream);
            Assert.Equal(project.Metadata.Name, loaded.Project.Metadata.Name);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
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
            service.CreateNew();
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
    [Trait("Category", "Unit")]
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
    [Trait("Category", "Unit")]
    public async Task SaveAsAsync_WhenPersistenceFails_PreservesDirtyStateAndCurrentPath()
    {
        var firstPath = CreateTemporaryPath();
        var secondPath = CreateTemporaryPath();
        try
        {
            var store = new ControllableProjectStore();
            var service = new ProjectDocumentService(store, CreateDocument);
            service.CreateNew();

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
        new(new JsonKeyboardProjectDocumentStore(), CreateDocument);

    private static KeyboardProjectDocument CreateDocument() =>
        new(DemoProjectFactory.Create(), BuildViewModel.CreateDefaultTargetProfiles());

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"KeyboardStudio-{Guid.NewGuid():N}.kbdproj");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class ControllableProjectStore : IKeyboardProjectDocumentStore
    {
        public bool FailOnSave { get; set; }

        public Task SaveAsync(
            KeyboardProjectDocument document,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            if (FailOnSave)
            {
                throw new IOException("Simulated save failure.");
            }

            return Task.CompletedTask;
        }

        public Task<KeyboardProjectDocument> LoadAsync(
            Stream source,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDocument());
    }
}
