using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public sealed class WindowsArtifactGenerator : IArtifactGenerator
{
    private readonly WindowsLayoutMetadata _metadata;

    public WindowsArtifactGenerator(WindowsLayoutMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        _metadata = metadata;
    }

    public Task<GeneratedArtifact> GenerateAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var model = WindowsLayoutTranslator.Translate(project);
        var source = WindowsCSourceGenerator.Generate(model, _metadata);
        GeneratedArtifact artifact = new(new GeneratedSource(new Dictionary<string, string>
        {
            ["keyboard.c"] = source
        }));

        return Task.FromResult(artifact);
    }
}
