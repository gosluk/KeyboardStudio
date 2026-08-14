using System.Text.Json;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

public sealed class JsonKeyboardProjectDocumentStore : IKeyboardProjectDocumentStore
{
    public const int CurrentDocumentSchemaVersion = 1;

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
                StringComparer.Ordinal)
        };

        await JsonSerializer.SerializeAsync(destination, dto, SerializerOptions, cancellationToken);
    }

    public async Task<KeyboardProjectDocument> LoadAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var dto = await JsonSerializer.DeserializeAsync<KeyboardProjectDocumentDto>(
            source,
            SerializerOptions,
            cancellationToken) ?? throw new InvalidDataException("The project document is empty.");
        if (dto.DocumentSchemaVersion != CurrentDocumentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Project document schema {dto.DocumentSchemaVersion} is not supported.");
        }

        var profiles = dto.Targets.ToDictionary(
            pair => RequireDiscriminator(pair.Key),
            pair => ToDomain(pair.Key, pair.Value),
            StringComparer.Ordinal);
        return new KeyboardProjectDocument(
            KeyboardProjectDtoMapper.ToDomain(dto.Project),
            profiles);
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
