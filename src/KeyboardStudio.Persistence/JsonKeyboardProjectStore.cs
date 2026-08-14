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

        await JsonSerializer.SerializeAsync(destination, project, SerializerOptions, cancellationToken);
    }

    public async Task<KeyboardProject> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var project = await JsonSerializer.DeserializeAsync<KeyboardProject>(source, SerializerOptions, cancellationToken);
        return project ?? throw new InvalidDataException("The project file does not contain a valid keyboard project.");
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
