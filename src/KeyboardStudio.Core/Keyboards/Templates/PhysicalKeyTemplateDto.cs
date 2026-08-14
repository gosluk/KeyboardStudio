namespace KeyboardStudio.Core;

public sealed class PhysicalKeyTemplateDto
{
    public required string Id { get; init; }
    public required int ScanCode { get; init; }
    public bool Extended { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
}
