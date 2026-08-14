using System.Text.Json.Serialization;

namespace KeyboardStudio.Persistence;

internal sealed class KeyboardProjectDto
{
    public int SchemaVersion { get; init; }
    public required ProjectMetadataDto Metadata { get; init; }
    public required PhysicalKeyboardDto Keyboard { get; init; }
    public required KeyboardLayoutDto Layout { get; init; }
}

internal sealed class ProjectMetadataDto
{
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public required string Version { get; init; }
    public required string Language { get; init; }
}

internal sealed class PhysicalKeyboardDto
{
    public required string Id { get; init; }
    public required List<PhysicalKeyDto> Keys { get; init; }
}

internal sealed class PhysicalKeyDto
{
    public required string Id { get; init; }
    public int ScanCode { get; init; }
    public bool Extended { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
}

internal sealed class KeyboardLayoutDto
{
    public required List<KeyMappingDto> Mappings { get; init; }
}

internal sealed class KeyMappingDto
{
    public required string KeyId { get; init; }
    public required string LogicalKey { get; init; }
    public required Dictionary<string, KeyOutputDto> Outputs { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CharacterOutputDto), "character")]
[JsonDerivedType(typeof(SpecialKeyOutputDto), "specialKey")]
[JsonDerivedType(typeof(NoOutputDto), "none")]
internal abstract record KeyOutputDto;

internal sealed record CharacterOutputDto(string Value) : KeyOutputDto;
internal sealed record SpecialKeyOutputDto(string Key) : KeyOutputDto;
internal sealed record NoOutputDto() : KeyOutputDto;
