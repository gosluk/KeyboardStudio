namespace KeyboardStudio.Build;

public sealed record ResolvedBuildEnvironment(
    BuildTarget Target,
    string CompilerPath,
    string LinkerPath,
    string ResourceCompilerPath,
    IReadOnlyList<string> IncludePaths,
    IReadOnlyList<string> LibraryPaths,
    string ToolVersion,
    string SdkVersion);
