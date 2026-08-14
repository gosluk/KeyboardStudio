using KeyboardStudio.Core;

namespace KeyboardStudio.Build;

public interface IBuildManifestWriter
{
    Task<BuildManifestWriteResult> WriteAsync(
        KeyboardProject project,
        GeneratedArtifact generatedArtifact,
        BuildOptions options,
        CompilationResult compilation,
        CancellationToken cancellationToken = default);
}
