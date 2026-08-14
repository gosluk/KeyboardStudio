using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardStudio.Core;

public sealed class KeyboardTemplateProvider : IKeyboardTemplateProvider
{
    private static readonly KeyboardTemplateDescriptor[] BuiltInTemplates =
    [
        new("iso-105", "ISO 105-key", 105, 54, 4),
        new("ansi-104", "ANSI 104-key", 104, 54, 4)
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly IKeyboardTemplateContentSource _contentSource;
    private readonly IReadOnlyDictionary<string, Lazy<CachedTemplate>> _templatesById;

    public KeyboardTemplateProvider()
        : this(new EmbeddedKeyboardTemplateContentSource(), BuiltInTemplates)
    {
    }

    public KeyboardTemplateProvider(
        IKeyboardTemplateContentSource contentSource,
        IEnumerable<KeyboardTemplateDescriptor> templates)
    {
        ArgumentNullException.ThrowIfNull(contentSource);
        ArgumentNullException.ThrowIfNull(templates);

        _contentSource = contentSource;

        var descriptors = templates.ToArray();
        if (descriptors.Length == 0)
        {
            throw new ArgumentException("At least one keyboard template descriptor is required.", nameof(templates));
        }

        var templatesById = new Dictionary<string, Lazy<CachedTemplate>>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors)
        {
            ValidateDescriptor(descriptor);

            if (!templatesById.TryAdd(
                    descriptor.Id,
                    new Lazy<CachedTemplate>(
                        () => LoadAndValidate(descriptor),
                        LazyThreadSafetyMode.ExecutionAndPublication)))
            {
                throw new ArgumentException(
                    $"Template descriptor ID '{descriptor.Id}' is registered more than once.",
                    nameof(templates));
            }
        }

        Templates = Array.AsReadOnly(descriptors);
        _templatesById = templatesById;
    }

    public IReadOnlyList<KeyboardTemplateDescriptor> Templates { get; }

    public PhysicalKeyboard Load(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            throw new ArgumentException("A template ID is required.", nameof(templateId));
        }

        if (!_templatesById.TryGetValue(templateId, out var template))
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.UnknownTemplate,
                templateId,
                $"Keyboard template '{templateId}' is not registered.");
        }

        return template.Value.CreateKeyboard();
    }

    private CachedTemplate LoadAndValidate(KeyboardTemplateDescriptor descriptor)
    {
        Stream stream;
        try
        {
            stream = _contentSource.OpenRead(descriptor.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.ResourceUnavailable,
                descriptor.Id,
                $"Keyboard template '{descriptor.Id}' could not be opened.",
                exception);
        }

        using (stream)
        {
            KeyboardTemplateDto? template;
            try
            {
                template = JsonSerializer.Deserialize<KeyboardTemplateDto>(stream, SerializerOptions);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                throw new KeyboardTemplateException(
                    KeyboardTemplateErrorCode.InvalidJson,
                    descriptor.Id,
                    $"Keyboard template '{descriptor.Id}' is not a valid template JSON document.",
                    exception);
            }

            if (template is null)
            {
                throw new KeyboardTemplateException(
                    KeyboardTemplateErrorCode.InvalidJson,
                    descriptor.Id,
                    $"Keyboard template '{descriptor.Id}' did not contain a template object.");
            }

            return ValidateAndConvert(descriptor, template);
        }
    }

    private static CachedTemplate ValidateAndConvert(
        KeyboardTemplateDescriptor descriptor,
        KeyboardTemplateDto template)
    {
        if (template.SchemaVersion != KeyboardTemplateSchema.CurrentVersion)
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.UnsupportedSchemaVersion,
                descriptor.Id,
                $"Keyboard template '{descriptor.Id}' uses schema version {template.SchemaVersion}; version {KeyboardTemplateSchema.CurrentVersion} is required.");
        }

        if (!string.Equals(template.Id, descriptor.Id, StringComparison.Ordinal) ||
            !string.Equals(template.Name, descriptor.Name, StringComparison.Ordinal))
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.TemplateIdentityMismatch,
                descriptor.Id,
                $"Keyboard template resource '{descriptor.Id}' does not match its registered ID and name.");
        }

        if (!IsValidTemplateId(template.Id) ||
            !double.IsFinite(template.UnitWidth) || template.UnitWidth <= 0 ||
            !double.IsFinite(template.UnitGap) || template.UnitGap < 0 ||
            template.UnitWidth != descriptor.UnitWidth ||
            template.UnitGap != descriptor.UnitGap)
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.InvalidTemplateMetadata,
                descriptor.Id,
                $"Keyboard template '{descriptor.Id}' contains invalid template metadata or rendering metrics.");
        }

        if (template.Keys is null)
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.InvalidJson,
                descriptor.Id,
                $"Keyboard template '{descriptor.Id}' must define a keys array.");
        }

        var keyIds = new HashSet<string>(StringComparer.Ordinal);
        var scanCodeIdentities = new HashSet<ScanCodeIdentity>();
        var keys = new List<PhysicalKey>(template.Keys.Count);

        foreach (var key in template.Keys)
        {
            if (key is null ||
                !IsValidPhysicalKeyId(key.Id) ||
                key.ScanCode is < 0 or > byte.MaxValue ||
                !double.IsFinite(key.X) || key.X < 0 ||
                !double.IsFinite(key.Y) || key.Y < 0 ||
                !double.IsFinite(key.Width) || key.Width <= 0 ||
                !double.IsFinite(key.Height) || key.Height <= 0)
            {
                throw new KeyboardTemplateException(
                    KeyboardTemplateErrorCode.InvalidKeyDefinition,
                    descriptor.Id,
                    $"Keyboard template '{descriptor.Id}' contains an invalid physical key definition.");
            }

            if (!keyIds.Add(key.Id))
            {
                throw new KeyboardTemplateException(
                    KeyboardTemplateErrorCode.DuplicateKeyId,
                    descriptor.Id,
                    $"Keyboard template '{descriptor.Id}' defines physical key ID '{key.Id}' more than once.");
            }

            var scanCodeIdentity = new ScanCodeIdentity(key.ScanCode, key.Extended);
            if (!scanCodeIdentities.Add(scanCodeIdentity))
            {
                var extendedSuffix = key.Extended ? " (extended)" : string.Empty;
                throw new KeyboardTemplateException(
                    KeyboardTemplateErrorCode.DuplicateScanCodeIdentity,
                    descriptor.Id,
                    $"Keyboard template '{descriptor.Id}' defines scan code 0x{key.ScanCode:X2}{extendedSuffix} more than once.");
            }

            keys.Add(new PhysicalKey
            {
                Id = key.Id,
                ScanCode = key.ScanCode,
                Extended = key.Extended,
                X = key.X,
                Y = key.Y,
                Width = key.Width,
                Height = key.Height
            });
        }

        if (keys.Count != descriptor.ExpectedKeyCount)
        {
            throw new KeyboardTemplateException(
                KeyboardTemplateErrorCode.IncompleteTemplate,
                descriptor.Id,
                $"Keyboard template '{descriptor.Id}' contains {keys.Count} keys; {descriptor.ExpectedKeyCount} are required.");
        }

        return new CachedTemplate(descriptor.Id, keys.ToArray());
    }

    private static void ValidateDescriptor(KeyboardTemplateDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!IsValidTemplateId(descriptor.Id))
        {
            throw new ArgumentException($"Template descriptor ID '{descriptor.Id}' is invalid.", nameof(descriptor));
        }

        if (string.IsNullOrWhiteSpace(descriptor.Name))
        {
            throw new ArgumentException("Template descriptor name is required.", nameof(descriptor));
        }

        if (descriptor.ExpectedKeyCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(descriptor),
                descriptor.ExpectedKeyCount,
                "Template expected key count must be greater than zero.");
        }

        if (!double.IsFinite(descriptor.UnitWidth) || descriptor.UnitWidth <= 0 ||
            !double.IsFinite(descriptor.UnitGap) || descriptor.UnitGap < 0)
        {
            throw new ArgumentException("Template descriptor rendering metrics are invalid.", nameof(descriptor));
        }
    }

    private static bool IsValidTemplateId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousWasDash = false;
        foreach (var character in value)
        {
            if (character == '-')
            {
                if (previousWasDash)
                {
                    return false;
                }

                previousWasDash = true;
                continue;
            }

            previousWasDash = false;
            if (!IsAsciiLower(character) && !IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidPhysicalKeyId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiLetter(character) &&
                !IsAsciiDigit(character) &&
                character is not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsAsciiLower(char value) => value is >= 'a' and <= 'z';

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private readonly record struct ScanCodeIdentity(int ScanCode, bool Extended);

    private sealed class CachedTemplate
    {
        private readonly string _id;
        private readonly PhysicalKey[] _keys;

        public CachedTemplate(string id, PhysicalKey[] keys)
        {
            _id = id;
            _keys = keys;
        }

        public PhysicalKeyboard CreateKeyboard() => new()
        {
            Id = _id,
            Keys = _keys.ToList()
        };
    }
}
