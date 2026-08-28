using System.Text.Json;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

/// <summary>
/// Reads seed projects from resources embedded in this assembly.
/// </summary>
/// <remarks>
/// Seeds are stored in the project file format and share the persistence DTOs, so a seed
/// is parsed by the same code that parses a user's <c>.kbdproj</c>. That is why this type
/// lives in the persistence assembly rather than beside <see cref="ISeedProjectSource"/>
/// in Core: Core owns the contract and must not gain knowledge of the storage format.
/// </remarks>
public sealed class EmbeddedSeedProjectSource : ISeedProjectSource
{
    private const string ResourcePrefix = "KeyboardStudio.Persistence.SeedProjects.";

    private static readonly string[] KnownSeedIds = [SeedProjectId.UsBasic];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, byte[]> _contentBySeedId = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public IReadOnlyList<string> SeedIds { get; } = Array.AsReadOnly(KnownSeedIds);

    public KeyboardProject Create(string seedId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seedId);

        if (!KnownSeedIds.Contains(seedId, StringComparer.Ordinal))
        {
            throw new SeedProjectException(seedId, $"Seed project '{seedId}' is not registered.");
        }

        var content = GetContent(seedId);

        KeyboardProjectDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<KeyboardProjectDto>(content, SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new SeedProjectException(
                seedId,
                $"Seed project '{seedId}' is not a valid project document.",
                exception);
        }

        if (dto is null)
        {
            throw new SeedProjectException(
                seedId,
                $"Seed project '{seedId}' did not contain a project object.");
        }

        if (dto.SchemaVersion != KeyboardProjectSchema.CurrentVersion)
        {
            throw new SeedProjectException(
                seedId,
                $"Seed project '{seedId}' uses schema version {dto.SchemaVersion}; version {KeyboardProjectSchema.CurrentVersion} is required.");
        }

        try
        {
            return KeyboardProjectDtoMapper.ToDomain(dto);
        }
        catch (InvalidDataException exception)
        {
            throw new SeedProjectException(
                seedId,
                $"Seed project '{seedId}' contains invalid project content.",
                exception);
        }
    }

    private byte[] GetContent(string seedId)
    {
        lock (_gate)
        {
            if (_contentBySeedId.TryGetValue(seedId, out var cached))
            {
                return cached;
            }

            var resourceName = $"{ResourcePrefix}{seedId}.kbdproj";
            using var stream = typeof(EmbeddedSeedProjectSource).Assembly
                .GetManifestResourceStream(resourceName)
                ?? throw new SeedProjectException(
                    seedId,
                    $"Embedded seed project resource '{resourceName}' was not found.");

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var content = buffer.ToArray();
            _contentBySeedId[seedId] = content;
            return content;
        }
    }
}
