using System.Text.Json;
using System.Text.Json.Nodes;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

public sealed class JsonKeyboardProjectStore : IKeyboardProjectStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly ProjectMigrationPipeline _migrationPipeline;

    public JsonKeyboardProjectStore()
        : this(new ProjectMigrationPipeline([]))
    {
    }

    public JsonKeyboardProjectStore(ProjectMigrationPipeline migrationPipeline)
    {
        ArgumentNullException.ThrowIfNull(migrationPipeline);
        _migrationPipeline = migrationPipeline;
    }

    public async Task SaveAsync(
        KeyboardProject project,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(destination);

        if (project.SchemaVersion != KeyboardProjectSchema.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Only project schema version {KeyboardProjectSchema.CurrentVersion} can be saved.");
        }

        var dto = KeyboardProjectDtoMapper.ToDto(project);
        await JsonSerializer.SerializeAsync(destination, dto, SerializerOptions, cancellationToken);
    }

    public async Task<KeyboardProject> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.InvalidJson,
                "The project file is not valid JSON.",
                innerException: exception);
        }

        using (document)
        {
            var schemaVersion = ReadAndValidateSchemaVersion(document.RootElement);

            try
            {
                var dto = schemaVersion == KeyboardProjectSchema.CurrentVersion
                    ? document.RootElement.Deserialize<KeyboardProjectDto>(SerializerOptions)
                    : DeserializeMigratedProject(document.RootElement, schemaVersion);

                if (dto is null)
                {
                    throw new ProjectLoadException(
                        ProjectLoadErrorCode.InvalidProject,
                        $"The project file does not contain a valid schema version {schemaVersion} project.",
                        schemaVersion);
                }

                return KeyboardProjectDtoMapper.ToDomain(dto);
            }
            catch (ProjectMigrationException exception)
            {
                throw new ProjectLoadException(
                    ProjectLoadErrorCode.LegacySchemaRequiresMigration,
                    $"Project schema version {schemaVersion} cannot be migrated to version {KeyboardProjectSchema.CurrentVersion} because a required migration is not registered.",
                    schemaVersion,
                    exception);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidDataException)
            {
                throw new ProjectLoadException(
                    ProjectLoadErrorCode.InvalidProject,
                    $"The project file does not match schema version {schemaVersion}.",
                    schemaVersion,
                    exception);
            }
        }
    }

    private KeyboardProjectDto? DeserializeMigratedProject(JsonElement root, int schemaVersion)
    {
        var legacyProject = JsonNode.Parse(root.GetRawText()) as JsonObject
            ?? throw new InvalidDataException("The legacy project root must be a JSON object.");

        var migratedProject = _migrationPipeline.Migrate(
            legacyProject,
            schemaVersion,
            KeyboardProjectSchema.CurrentVersion);

        return migratedProject.Deserialize<KeyboardProjectDto>(SerializerOptions);
    }

    private static int ReadAndValidateSchemaVersion(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.InvalidProject,
                "The project file root must be a JSON object.");
        }

        if (!TryGetProperty(root, "schemaVersion", out var schemaElement))
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.MissingSchemaVersion,
                "The project file is missing the required 'schemaVersion' property.");
        }

        if (schemaElement.ValueKind != JsonValueKind.Number || !schemaElement.TryGetInt32(out var schemaVersion))
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.InvalidSchemaVersion,
                "The project 'schemaVersion' must be an integer.");
        }

        if (schemaVersion < KeyboardProjectSchema.FirstVersion)
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.InvalidSchemaVersion,
                $"Project schema version {schemaVersion} is invalid. The first valid version is {KeyboardProjectSchema.FirstVersion}.",
                schemaVersion);
        }

        if (schemaVersion > KeyboardProjectSchema.CurrentVersion)
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.UnsupportedFutureSchema,
                $"Project schema version {schemaVersion} is newer than the supported version {KeyboardProjectSchema.CurrentVersion}.",
                schemaVersion);
        }

        return schemaVersion;
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static JsonSerializerOptions CreateSerializerOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}
