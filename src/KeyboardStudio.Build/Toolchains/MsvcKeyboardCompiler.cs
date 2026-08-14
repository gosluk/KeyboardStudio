namespace KeyboardStudio.Build;

public sealed class MsvcKeyboardCompiler : INativeCompiler
{
    private readonly IBuildEnvironment _environment;
    private readonly IProcessRunner _processRunner;

    public MsvcKeyboardCompiler()
        : this(new WindowsBuildEnvironment(), new ProcessRunner())
    {
    }

    public MsvcKeyboardCompiler(IBuildEnvironment environment, IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(processRunner);
        _environment = environment;
        _processRunner = processRunner;
    }

    public async Task<CompilationResult> CompileAsync(
        GeneratedArtifact artifact,
        BuildOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var toolchain = _environment.Resolve(options.Target);
        if (toolchain is null)
        {
            var status = _environment.GetStatus(options.Target);
            return new CompilationResult(
                false,
                null,
                [new CompilerMessage("ENV001", status.Message)]);
        }

        if (!artifact.Source.Files.ContainsKey("keyboard.c"))
        {
            return new CompilationResult(
                false,
                null,
                [new CompilerMessage("MSVC_SOURCE", "Generated source does not contain keyboard.c.")]);
        }

        var workspace = BuildWorkspace.Create(options.OutputDirectory);
        await workspace.WriteGeneratedSourceAsync(artifact.Source, cancellationToken);
        var objectPath = Path.Combine(workspace.ObjectDirectory, "keyboard.obj");
        var compileRequest = CreateCompileRequest(toolchain, workspace, objectPath);
        var compileResult = await _processRunner.RunAsync(compileRequest, cancellationToken);
        if (compileResult.ExitCode != 0)
        {
            return new CompilationResult(
                false,
                null,
                [new CompilerMessage("MSVC_CL", SelectToolOutput(compileResult))]);
        }

        return new CompilationResult(true, objectPath, []);
    }

    private static ProcessRequest CreateCompileRequest(
        ResolvedBuildEnvironment toolchain,
        BuildWorkspace workspace,
        string objectPath)
    {
        var arguments = new List<string>
        {
            "/nologo",
            "/c",
            "/Zl",
            "/W4",
            "/WX",
            "/O2",
            "/GS-",
            "/Gy",
            "/DWIN32",
            "/D_WINDOWS",
            toolchain.Target == BuildTarget.WindowsX64 ? "/D_WIN64" : "/D_ARM64_",
            $"/Fo{objectPath}"
        };
        arguments.AddRange(toolchain.IncludePaths.Select(path => $"/I{path}"));
        arguments.Add(Path.Combine(workspace.GeneratedDirectory, "keyboard.c"));

        return new ProcessRequest(
            toolchain.CompilerPath,
            arguments,
            workspace.RootDirectory,
            CreateToolEnvironment(toolchain));
    }

    private static IReadOnlyDictionary<string, string?> CreateToolEnvironment(
        ResolvedBuildEnvironment toolchain) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["INCLUDE"] = string.Join(Path.PathSeparator, toolchain.IncludePaths),
            ["LIB"] = string.Join(Path.PathSeparator, toolchain.LibraryPaths)
        };

    private static string SelectToolOutput(ProcessResult result)
    {
        var output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return string.IsNullOrWhiteSpace(output)
            ? $"{Path.GetFileName(result.Executable)} exited with code {result.ExitCode}."
            : output.Trim();
    }
}
