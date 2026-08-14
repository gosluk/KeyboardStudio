using KeyboardStudio.Build;
using KeyboardStudio.Core;

namespace KeyboardStudio.Windows;

public sealed class WindowsArtifactGenerator : IArtifactGenerator
{
    public Task<GeneratedArtifact> GenerateAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        cancellationToken.ThrowIfCancellationRequested();

        var model = WindowsLayoutTranslator.Translate(project);
        var source = WindowsCSourceGenerator.Generate(model, project.Metadata.Name);
        GeneratedArtifact artifact = new(new GeneratedSource(new Dictionary<string, string>
        {
            ["keyboard.c"] = source
        }));

        return Task.FromResult(artifact);
    }
}
