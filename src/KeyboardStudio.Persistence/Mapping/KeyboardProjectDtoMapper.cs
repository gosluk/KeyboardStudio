using System.Text.Json;
using KeyboardStudio.Core;

namespace KeyboardStudio.Persistence;

internal static class KeyboardProjectDtoMapper
{
    private static readonly JsonNamingPolicy EnumNamingPolicy = JsonNamingPolicy.CamelCase;

    public static KeyboardProjectDto ToDto(KeyboardProject project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new KeyboardProjectDto
        {
            SchemaVersion = project.SchemaVersion,
            Metadata = new ProjectMetadataDto
            {
                Name = project.Metadata.Name,
                Description = project.Metadata.Description,
                Version = project.Metadata.Version,
                Language = project.Metadata.Language
            },
            Keyboard = new PhysicalKeyboardDto
            {
                Id = project.Keyboard.Id,
                Keys = project.Keyboard.Keys.Select(ToDto).ToList()
            },
            Layout = new KeyboardLayoutDto
            {
                Mappings = project.Layout.Mappings.Select(ToDto).ToList()
            }
        };
    }

    public static KeyboardProject ToDomain(KeyboardProjectDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var metadata = Require(dto.Metadata, "metadata");
        var keyboard = Require(dto.Keyboard, "keyboard");
        var layout = Require(dto.Layout, "layout");

        return new KeyboardProject
        {
            SchemaVersion = dto.SchemaVersion,
            Metadata = new ProjectMetadata
            {
                Name = RequireText(metadata.Name, "metadata.name"),
                Description = metadata.Description ?? string.Empty,
                Version = RequireText(metadata.Version, "metadata.version"),
                Language = RequireText(metadata.Language, "metadata.language")
            },
            Keyboard = new PhysicalKeyboard
            {
                Id = RequireText(keyboard.Id, "keyboard.id"),
                Keys = Require(keyboard.Keys, "keyboard.keys").Select(ToDomain).ToList()
            },
            Layout = new KeyboardLayout
            {
                Mappings = Require(layout.Mappings, "layout.mappings").Select(ToDomain).ToList()
            }
        };
    }

    private static PhysicalKeyDto ToDto(PhysicalKey key) => new()
    {
        Id = key.Id,
        ScanCode = key.ScanCode,
        Extended = key.Extended,
        X = key.X,
        Y = key.Y,
        Width = key.Width,
        Height = key.Height
    };

    private static PhysicalKey ToDomain(PhysicalKeyDto dto) => new()
    {
        Id = RequireText(dto.Id, "keyboard.keys[].id"),
        ScanCode = dto.ScanCode,
        Extended = dto.Extended,
        X = dto.X,
        Y = dto.Y,
        Width = dto.Width,
        Height = dto.Height
    };

    private static KeyMappingDto ToDto(KeyMapping mapping) => new()
    {
        KeyId = mapping.KeyId,
        LogicalKey = FormatEnum(mapping.LogicalKey),
        Outputs = mapping.Outputs.ToDictionary(
            pair => FormatEnum(pair.Key),
            pair => ToDto(pair.Value),
            StringComparer.Ordinal)
    };

    private static KeyMapping ToDomain(KeyMappingDto dto)
    {
        var outputs = new Dictionary<ModifierLayer, KeyOutput>();
        foreach (var pair in Require(dto.Outputs, "layout.mappings[].outputs"))
        {
            var layer = ParseEnum<ModifierLayer>(pair.Key, "layout.mappings[].outputs key");
            if (pair.Value is null)
            {
                throw new InvalidDataException($"Output for modifier layer '{pair.Key}' must not be null.");
            }

            if (!outputs.TryAdd(layer, ToDomain(pair.Value)))
            {
                throw new InvalidDataException($"Modifier layer '{pair.Key}' is defined more than once.");
            }
        }

        return new KeyMapping
        {
            KeyId = RequireText(dto.KeyId, "layout.mappings[].keyId"),
            LogicalKey = ParseEnum<LogicalKey>(dto.LogicalKey, "layout.mappings[].logicalKey"),
            Outputs = outputs
        };
    }

    private static KeyOutputDto ToDto(KeyOutput output) => output switch
    {
        CharacterOutput character => new CharacterOutputDto(character.Value),
        SpecialKeyOutput specialKey => new SpecialKeyOutputDto(FormatEnum(specialKey.Key)),
        NoOutput => new NoOutputDto(),
        _ => throw new NotSupportedException($"Key output type '{output.GetType().Name}' is not supported by the project format.")
    };

    private static KeyOutput ToDomain(KeyOutputDto dto) => dto switch
    {
        CharacterOutputDto character => new CharacterOutput(RequireText(character.Value, "character output value")),
        SpecialKeyOutputDto specialKey => new SpecialKeyOutput(ParseEnum<LogicalKey>(specialKey.Key, "special-key output key")),
        NoOutputDto => new NoOutput(),
        _ => throw new InvalidDataException($"Key output DTO type '{dto.GetType().Name}' is not supported.")
    };

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum => EnumNamingPolicy.ConvertName(value.ToString());

    private static TEnum ParseEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct, Enum
    {
        var text = RequireText(value, fieldName);
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(FormatEnum(candidate), text, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidDataException($"Value '{text}' is not a supported {typeof(TEnum).Name} for '{fieldName}'.");
    }

    private static T Require<T>(T? value, string fieldName)
        where T : class => value ?? throw new InvalidDataException($"Required project field '{fieldName}' must not be null.");

    private static string RequireText(string? value, string fieldName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException($"Required project field '{fieldName}' must not be empty.")
            : value;
}
