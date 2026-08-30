namespace KeyboardStudio.Persistence;

internal sealed class LayoutDerivationDto
{
    public required string ProjectInstallationId { get; init; }

    public required string SourceId { get; init; }

    public required string SourceOrigin { get; init; }

    public required string BaseLayoutId { get; init; }

    public string? BaseVariantId { get; init; }

    public required string ResolvedBaseSectionId { get; init; }

    public DateTimeOffset ImportedAtUtc { get; init; }

    public required string ImportFidelity { get; init; }

    public List<LayoutDerivationMappingDto> BaselineMappings { get; init; } = [];

    public string? SourceFingerprint { get; init; }

    public string? IncludeChainFingerprint { get; init; }
}
