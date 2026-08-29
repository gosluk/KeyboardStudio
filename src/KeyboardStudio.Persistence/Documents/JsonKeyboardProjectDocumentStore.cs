using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

public sealed class JsonKeyboardProjectDocumentStore : IKeyboardProjectDocumentStore
{
    /// <summary>The first envelope version, and so the oldest one that can still be opened.</summary>
    public const int FirstDocumentSchemaVersion = 1;

    /// <summary>
    /// The envelope version written today. Version 2 added <c>importProvenance</c>.
    /// </summary>
    public const int CurrentDocumentSchemaVersion = 2;

    /// <summary>
    /// One step up the envelope schema, keyed by the version it reads. Each step receives the raw
    /// JSON of its own version and returns the next version's, so the current DTO contract only
    /// ever sees the shape it was written for.
    ///
    /// The steps work on JSON rather than on DTOs for the same reason the project migrations do: a
    /// DTO describes today's format, and a historical document is by definition not in it.
    /// </summary>
    private static readonly FrozenDictionary<int, Func<JsonObject, JsonObject>> Migrations =
        new Dictionary<int, Func<JsonObject, JsonObject>>
        {
            // 1 -> 2 added importProvenance, which is optional: a document written before import
            // existed has no import to record, and its absence already reads as "not imported".
            // The step is registered rather than skipped so the chain has no gap for version 3 to
            // fall through.
            [1] = static document => document
        }.ToFrozenDictionary();

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task SaveAsync(
        KeyboardProjectDocument document,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);

        var dto = new KeyboardProjectDocumentDto
        {
            DocumentSchemaVersion = CurrentDocumentSchemaVersion,
            Project = KeyboardProjectDtoMapper.ToDto(document.Project),
            Targets = document.TargetProfiles.ToDictionary(
                pair => RequireDiscriminator(pair.Key),
                pair => ToDto(pair.Value),
                StringComparer.Ordinal),
            ImportProvenance = document.ImportProvenance is null
                ? null
                : ToDto(document.ImportProvenance)
        };

        await JsonSerializer.SerializeAsync(destination, dto, SerializerOptions, cancellationToken);
    }

    public async Task<KeyboardProjectDocument> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var json = await JsonDocument.ParseAsync(source, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("documentSchemaVersion", out var versionElement))
        {
            return await LoadLegacyProjectAsync(json.RootElement, cancellationToken);
        }

        var dto = Deserialize(json.RootElement, ReadSchemaVersion(versionElement))
            ?? throw new InvalidDataException("The project document is empty.");

        var profiles = dto.Targets.ToDictionary(
            pair => RequireDiscriminator(pair.Key),
            pair => ToDomain(pair.Key, pair.Value),
            StringComparer.Ordinal);
        return new KeyboardProjectDocument(
            KeyboardProjectDtoMapper.ToDomain(dto.Project),
            profiles,
            dto.ImportProvenance is null ? null : ToDomain(dto.ImportProvenance));
    }

    private static int ReadSchemaVersion(JsonElement versionElement)
    {
        if (versionElement.ValueKind != JsonValueKind.Number ||
            !versionElement.TryGetInt32(out var version))
        {
            throw new InvalidDataException("The 'documentSchemaVersion' must be an integer.");
        }

        if (version < FirstDocumentSchemaVersion || version > CurrentDocumentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Project document schema {version} is not supported.");
        }

        return version;
    }

    /// <summary>
    /// Reads a document of any supported version, migrating it up to the current one first. The
    /// current version skips the JSON round trip entirely, which is the case that runs every time
    /// the user opens a file they saved.
    /// </summary>
    private static KeyboardProjectDocumentDto? Deserialize(JsonElement root, int version)
    {
        if (version == CurrentDocumentSchemaVersion)
        {
            return root.Deserialize<KeyboardProjectDocumentDto>(SerializerOptions);
        }

        var document = JsonNode.Parse(root.GetRawText()) as JsonObject
            ?? throw new InvalidDataException("The project document root must be a JSON object.");

        while (version < CurrentDocumentSchemaVersion)
        {
            if (!Migrations.TryGetValue(version, out var migration))
            {
                throw new InvalidDataException(
                    $"No migration is registered from project document schema {version}.");
            }

            document = migration(document);
            version++;
            document["documentSchemaVersion"] = version;
        }

        return document.Deserialize<KeyboardProjectDocumentDto>(SerializerOptions);
    }

    private static async Task<KeyboardProjectDocument> LoadLegacyProjectAsync(
        JsonElement root,
        CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(root));
        var project = await new JsonKeyboardProjectStore().LoadAsync(stream, cancellationToken);
        return new KeyboardProjectDocument(
            project,
            new Dictionary<string, ProjectTargetProfile>(StringComparer.Ordinal));
    }

    private static ProjectTargetProfileDto ToDto(ProjectTargetProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return new ProjectTargetProfileDto
        {
            Target = RequireDiscriminator(profile.Target),
            Settings = new Dictionary<string, string>(profile.Settings, StringComparer.Ordinal)
        };
    }

    private static ProjectTargetProfile ToDomain(string key, ProjectTargetProfileDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var target = RequireDiscriminator(profile.Target);
        if (!string.Equals(key, target, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Target profile key '{key}' does not match discriminator '{target}'.");
        }

        return new ProjectTargetProfile(
            target,
            new Dictionary<string, string>(profile.Settings, StringComparer.Ordinal));
    }

    private static LayoutImportProvenanceDto ToDto(LayoutImportProvenance provenance) => new()
    {
        SourceId = RequireProvenanceIdentifier(provenance.SourceId, nameof(provenance.SourceId)),
        LayoutId = RequireProvenanceIdentifier(provenance.LayoutId, nameof(provenance.LayoutId)),
        VariantId = provenance.VariantId,
        SourceLocation = provenance.SourceLocation,
        SourceDescription = provenance.SourceDescription,
        ImportedAtUtc = provenance.ImportedAtUtc
    };

    private static LayoutImportProvenance ToDomain(LayoutImportProvenanceDto provenance) =>
        new(RequireProvenanceIdentifier(provenance.SourceId, nameof(provenance.SourceId)),
            RequireProvenanceIdentifier(provenance.LayoutId, nameof(provenance.LayoutId)),
            provenance.VariantId,
            provenance.SourceLocation,
            provenance.SourceDescription,
            provenance.ImportedAtUtc);

    private static string RequireProvenanceIdentifier(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Import provenance '{name}' must not be empty.");
        }

        return value;
    }

    private static string RequireDiscriminator(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidDataException(
                "A target discriminator must use only ASCII letters, digits, '-' or '_'.");
        }

        return value;
    }
}
