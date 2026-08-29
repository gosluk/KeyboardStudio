namespace KeyboardStudio.Persistence;

internal sealed class LayoutImportProvenanceDto
{
    public required string SourceId { get; init; }

    public required string LayoutId { get; init; }

    public string? VariantId { get; init; }

    public string? SourceLocation { get; init; }

    public string? SourceDescription { get; init; }

    public DateTimeOffset ImportedAtUtc { get; init; }
}
