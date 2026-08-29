namespace KeyboardStudio.Persistence;

internal sealed class KeyboardProjectDocumentDto
{
    public int DocumentSchemaVersion { get; init; }

    public required KeyboardProjectDto Project { get; init; }

    public Dictionary<string, ProjectTargetProfileDto> Targets { get; init; } = [];

    public LayoutImportProvenanceDto? ImportProvenance { get; init; }
}
