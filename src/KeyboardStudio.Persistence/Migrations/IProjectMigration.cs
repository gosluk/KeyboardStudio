using System.Text.Json.Nodes;

namespace KeyboardStudio.Persistence;

public interface IProjectMigration
{
    int FromVersion { get; }
    int ToVersion { get; }

    JsonObject Migrate(JsonObject project);
}
