namespace KeyboardStudio.Persistence;

internal sealed class ProjectTargetProfileDto
{
    public required string Target { get; init; }

    public Dictionary<string, string> Settings { get; init; } = [];
}
