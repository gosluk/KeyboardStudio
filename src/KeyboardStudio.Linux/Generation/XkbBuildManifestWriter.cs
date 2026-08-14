using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardStudio.Linux;

public sealed class XkbBuildManifestWriter : IXkbBuildManifestWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public async Task<string> WriteAsync(
        XkbBuildManifest manifest,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        var path = Path.Combine(outputRoot, "build-manifest.json");
        var json = JsonSerializer.Serialize(manifest, SerializerOptions) + "\n";
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false), cancellationToken);
        return path;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
