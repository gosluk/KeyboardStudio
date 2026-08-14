using System.Text.Json;
using System.Text.Json.Serialization;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

public sealed class JsonKeyboardProjectStore : IKeyboardProjectStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

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

        await JsonSerializer.SerializeAsync(destination, project, SerializerOptions, cancellationToken);
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
                var project = document.RootElement.Deserialize<KeyboardProject>(SerializerOptions);
                return project ?? throw new ProjectLoadException(
                    ProjectLoadErrorCode.InvalidProject,
                    $"The project file does not contain a valid schema version {schemaVersion} project.",
                    schemaVersion);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                throw new ProjectLoadException(
                    ProjectLoadErrorCode.InvalidProject,
                    $"The project file does not match schema version {schemaVersion}.",
                    schemaVersion,
                    exception);
            }
        }
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

        if (schemaVersion < KeyboardProjectSchema.CurrentVersion)
        {
            throw new ProjectLoadException(
                ProjectLoadErrorCode.LegacySchemaRequiresMigration,
                $"Project schema version {schemaVersion} requires migration to version {KeyboardProjectSchema.CurrentVersion}.",
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

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
