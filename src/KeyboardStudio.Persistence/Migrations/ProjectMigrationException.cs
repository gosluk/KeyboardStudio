namespace KeyboardStudio.Persistence;

public sealed class ProjectMigrationException : Exception
{
    public ProjectMigrationException(int currentVersion, int targetVersion)
        : base($"No project migration is registered from schema version {currentVersion} while migrating to version {targetVersion}.")
    {
        CurrentVersion = currentVersion;
        TargetVersion = targetVersion;
    }

    public int CurrentVersion { get; }

    public int TargetVersion { get; }
}
