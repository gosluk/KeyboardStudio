using System.Text.Json.Nodes;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

public sealed class ProjectMigrationPipeline
{
    private readonly IReadOnlyDictionary<int, IProjectMigration> _migrationsBySourceVersion;

    public ProjectMigrationPipeline(IEnumerable<IProjectMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(migrations);

        var migrationsBySourceVersion = new Dictionary<int, IProjectMigration>();
        foreach (var migration in migrations)
        {
            ArgumentNullException.ThrowIfNull(migration);

            if (migration.FromVersion < KeyboardProjectSchema.FirstVersion)
            {
                throw new InvalidOperationException(
                    $"Migration source version {migration.FromVersion} is older than the first supported project schema version {KeyboardProjectSchema.FirstVersion}.");
            }

            if (migration.ToVersion != migration.FromVersion + 1)
            {
                throw new InvalidOperationException(
                    $"Project migrations must advance exactly one schema version. Migration {migration.FromVersion} -> {migration.ToVersion} is invalid.");
            }

            if (!migrationsBySourceVersion.TryAdd(migration.FromVersion, migration))
            {
                throw new InvalidOperationException(
                    $"More than one project migration is registered from schema version {migration.FromVersion}.");
            }
        }

        _migrationsBySourceVersion = migrationsBySourceVersion;
    }

    public JsonObject Migrate(JsonObject project, int sourceVersion, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (sourceVersion < KeyboardProjectSchema.FirstVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceVersion),
                sourceVersion,
                $"The source schema version must be at least {KeyboardProjectSchema.FirstVersion}.");
        }

        if (targetVersion < sourceVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetVersion),
                targetVersion,
                "The target schema version cannot be older than the source schema version.");
        }

        var migratedProject = (JsonObject)project.DeepClone();
        var currentVersion = sourceVersion;

        while (currentVersion < targetVersion)
        {
            if (!_migrationsBySourceVersion.TryGetValue(currentVersion, out var migration))
            {
                throw new ProjectMigrationException(currentVersion, targetVersion);
            }

            migratedProject = migration.Migrate(migratedProject)
                ?? throw new InvalidOperationException(
                    $"Project migration {migration.FromVersion} -> {migration.ToVersion} returned null.");

            currentVersion = migration.ToVersion;
            migratedProject["schemaVersion"] = currentVersion;
        }

        return migratedProject;
    }
}
