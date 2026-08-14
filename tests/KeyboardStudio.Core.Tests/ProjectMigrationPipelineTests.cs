using System.Text.Json.Nodes;
using KeyboardStudio.Persistence;
using Xunit;

namespace KeyboardStudio.Core.Tests;

public sealed class ProjectMigrationPipelineTests
{
    [Fact]
    public void Migrate_WhenSourceAlreadyMatchesTarget_ReturnsEquivalentClone()
    {
        var source = JsonNode.Parse("""{"schemaVersion":1,"name":"demo"}""")!.AsObject();
        var pipeline = new ProjectMigrationPipeline([]);

        var migrated = pipeline.Migrate(source, 1, 1);

        Assert.NotSame(source, migrated);
        Assert.Equal(1, migrated["schemaVersion"]!.GetValue<int>());
        Assert.Equal("demo", migrated["name"]!.GetValue<string>());
    }

    [Fact]
    public void Migrate_WhenSequentialMigrationsExist_AppliesThemInOrderAndAdvancesSchema()
    {
        var source = JsonNode.Parse("""{"schemaVersion":1}""")!.AsObject();
        var pipeline = new ProjectMigrationPipeline(
        [
            new Version1To2Migration(),
            new Version2To3Migration()
        ]);

        var migrated = pipeline.Migrate(source, 1, 3);

        Assert.Equal(3, migrated["schemaVersion"]!.GetValue<int>());
        Assert.True(migrated["addedInV2"]!.GetValue<bool>());
        Assert.True(migrated["v3SawV2"]!.GetValue<bool>());
        Assert.False(source.ContainsKey("addedInV2"));
    }

    [Fact]
    public void Migrate_WhenMigrationStepIsMissing_ReportsMissingVersion()
    {
        var source = JsonNode.Parse("""{"schemaVersion":1}""")!.AsObject();
        var pipeline = new ProjectMigrationPipeline([new Version1To2Migration()]);

        var exception = Assert.Throws<ProjectMigrationException>(() => pipeline.Migrate(source, 1, 3));

        Assert.Equal(2, exception.CurrentVersion);
        Assert.Equal(3, exception.TargetVersion);
    }

    [Fact]
    public void Constructor_WhenMigrationSkipsVersion_RejectsConfiguration()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ProjectMigrationPipeline([new Version1To3Migration()]));
    }

    [Fact]
    public void Constructor_WhenTwoMigrationsShareSourceVersion_RejectsConfiguration()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ProjectMigrationPipeline(
            [
                new Version1To2Migration(),
                new AlternateVersion1To2Migration()
            ]));
    }

    private sealed class Version1To2Migration : IProjectMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public JsonObject Migrate(JsonObject project)
        {
            project["addedInV2"] = true;
            return project;
        }
    }

    private sealed class AlternateVersion1To2Migration : IProjectMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 2;

        public JsonObject Migrate(JsonObject project) => project;
    }

    private sealed class Version2To3Migration : IProjectMigration
    {
        public int FromVersion => 2;

        public int ToVersion => 3;

        public JsonObject Migrate(JsonObject project)
        {
            project["v3SawV2"] = project["addedInV2"]?.GetValue<bool>() == true;
            return project;
        }
    }

    private sealed class Version1To3Migration : IProjectMigration
    {
        public int FromVersion => 1;

        public int ToVersion => 3;

        public JsonObject Migrate(JsonObject project) => project;
    }
}
