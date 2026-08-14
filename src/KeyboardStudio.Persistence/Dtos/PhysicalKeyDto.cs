namespace KeyboardStudio.Persistence;

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
