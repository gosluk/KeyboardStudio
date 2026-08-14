namespace KeyboardStudio.Core;

public sealed class KeyboardTemplateDto
{
    public required int SchemaVersion { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required double UnitWidth { get; init; }
    public required double UnitGap { get; init; }
    public required List<PhysicalKeyTemplateDto> Keys { get; init; }
}
