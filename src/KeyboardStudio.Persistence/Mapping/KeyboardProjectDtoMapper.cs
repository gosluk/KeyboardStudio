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
        CharacterOutput character => new KeyOutputDto
        {
            Kind = KeyOutputKinds.Character,
            Value = character.Value
        },
        SpecialKeyOutput specialKey => new KeyOutputDto
        {
            Kind = KeyOutputKinds.SpecialKey,
            Key = FormatEnum(specialKey.Key)
        },
        NoOutput => new KeyOutputDto
        {
            Kind = KeyOutputKinds.None
        },
        _ => throw new NotSupportedException($"Key output type '{output.GetType().Name}' is not supported by the project format.")
    };

    private static KeyOutput ToDomain(KeyOutputDto dto)
    {
        var kind = RequireText(dto.Kind, "layout.mappings[].outputs[].kind");
        return kind switch
        {
            KeyOutputKinds.Character => ToCharacterOutput(dto),
            KeyOutputKinds.SpecialKey => ToSpecialKeyOutput(dto),
            KeyOutputKinds.None => ToNoOutput(dto),
            _ => throw new InvalidDataException($"Output kind '{kind}' is not supported by the project format.")
        };
    }

    private static CharacterOutput ToCharacterOutput(KeyOutputDto dto)
    {
        RequireAbsent(dto.Key, "key", KeyOutputKinds.Character);
        var value = RequireNonEmpty(dto.Value, "character output value");
        try
        {
            return new CharacterOutput(value);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "A persisted character output must contain exactly one Unicode scalar value.",
                exception);
        }
    }

    private static SpecialKeyOutput ToSpecialKeyOutput(KeyOutputDto dto)
    {
        RequireAbsent(dto.Value, "value", KeyOutputKinds.SpecialKey);
        return new SpecialKeyOutput(ParseEnum<LogicalKey>(dto.Key, "special-key output key"));
    }

    private static NoOutput ToNoOutput(KeyOutputDto dto)
    {
        RequireAbsent(dto.Value, "value", KeyOutputKinds.None);
        RequireAbsent(dto.Key, "key", KeyOutputKinds.None);
        return new NoOutput();
    }

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

    private static string RequireNonEmpty(string? value, string fieldName) =>
        string.IsNullOrEmpty(value)
            ? throw new InvalidDataException($"Required project field '{fieldName}' must not be empty.")
            : value;

    private static void RequireAbsent(string? value, string fieldName, string kind)
    {
        if (value is not null)
        {
            throw new InvalidDataException($"Output kind '{kind}' must not define '{fieldName}'.");
        }
    }
}
