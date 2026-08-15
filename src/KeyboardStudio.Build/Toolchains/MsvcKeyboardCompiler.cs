using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KeyboardStudio.Build;

public sealed class MsvcKeyboardCompiler : INativeCompiler
{
    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IBuildEnvironment _environment;
    private readonly IProcessRunner _processRunner;
    private readonly IArtifactVerifier _artifactVerifier;

    public MsvcKeyboardCompiler()
        : this(
            new WindowsBuildEnvironment(),
            new ProcessRunner(),
            new PortableExecutableArtifactVerifier())
    {
    }

    public MsvcKeyboardCompiler(
        IBuildEnvironment environment,
        IProcessRunner processRunner,
        IArtifactVerifier? artifactVerifier = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(processRunner);
        _environment = environment;
        _processRunner = processRunner;
        _artifactVerifier = artifactVerifier ?? new PortableExecutableArtifactVerifier();
    }

    public async Task<CompilationResult> CompileAsync(
        GeneratedArtifact artifact,
        BuildOptions options,
        IProgress<BuildStageProgress>? progress = null,
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

        var toolchainVersions = new BuildToolchainVersions(
            toolchain.ToolVersion,
            toolchain.SdkVersion);

        var requiredSourceFiles = new[] { "keyboard.c", "keyboard.def", "keyboard.rc" };
        var missingSourceFiles = requiredSourceFiles
            .Where(fileName => !artifact.Source.Files.ContainsKey(fileName))
            .ToArray();
        if (missingSourceFiles.Length > 0)
        {
            return new CompilationResult(
                false,
                null,
                [new CompilerMessage(
                    "MSVC_SOURCE",
                    $"Generated source is missing: {string.Join(", ", missingSourceFiles)}.")]);
        }

        ValidateOutputFileName(artifact.OutputFileName);

        var workspace = BuildWorkspace.Create(options.OutputDirectory);
        try
        {
            await workspace.WriteGeneratedSourceAsync(artifact.Source, cancellationToken);
            var processResults = new List<ProcessResult>();
            var objectPath = Path.Combine(workspace.ObjectDirectory, "keyboard.obj");
            progress?.Report(new BuildStageProgress(BuildStageNames.Compiling, BuildStageState.Running));
            var compileRequest = CreateCompileRequest(toolchain, workspace, objectPath);
            var compileResult = await _processRunner.RunAsync(compileRequest, cancellationToken);
            processResults.Add(compileResult);
            if (compileResult.ExitCode != 0)
            {
                progress?.Report(new BuildStageProgress(BuildStageNames.Compiling, BuildStageState.Failed));
                return await CompleteAsync(
                    false,
                    null,
                    workspace,
                    processResults,
                    "MSVC_CL",
                    options.Target,
                    options.CleanupPolicy,
                    null,
                    toolchainVersions,
                    cancellationToken);
            }

            progress?.Report(new BuildStageProgress(BuildStageNames.Compiling, BuildStageState.Completed));
            progress?.Report(new BuildStageProgress(BuildStageNames.Linking, BuildStageState.Running));

            var resourcePath = Path.Combine(workspace.ObjectDirectory, "keyboard.res");
            var resourceResult = await _processRunner.RunAsync(
                CreateResourceRequest(toolchain, workspace, resourcePath),
                cancellationToken);
            processResults.Add(resourceResult);
            if (resourceResult.ExitCode != 0)
            {
                progress?.Report(new BuildStageProgress(BuildStageNames.Linking, BuildStageState.Failed));
                return await CompleteAsync(
                    false,
                    null,
                    workspace,
                    processResults,
                    "MSVC_RC",
                    options.Target,
                    options.CleanupPolicy,
                    null,
                    toolchainVersions,
                    cancellationToken);
            }

            var outputPath = Path.Combine(workspace.OutputDirectory, artifact.OutputFileName);
            var linkResult = await _processRunner.RunAsync(
                CreateLinkRequest(toolchain, workspace, objectPath, resourcePath, outputPath),
                cancellationToken);
            processResults.Add(linkResult);
            if (linkResult.ExitCode != 0)
            {
                progress?.Report(new BuildStageProgress(BuildStageNames.Linking, BuildStageState.Failed));
                return await CompleteAsync(
                    false,
                    null,
                    workspace,
                    processResults,
                    "MSVC_LINK",
                    options.Target,
                    options.CleanupPolicy,
                    null,
                    toolchainVersions,
                    cancellationToken);
            }

            progress?.Report(new BuildStageProgress(BuildStageNames.Linking, BuildStageState.Completed));
            progress?.Report(new BuildStageProgress(BuildStageNames.Verifying, BuildStageState.Running));

            var verification = await _artifactVerifier.VerifyAsync(
                outputPath,
                options.Target,
                cancellationToken);
            progress?.Report(new BuildStageProgress(
                BuildStageNames.Verifying,
                verification.Success ? BuildStageState.Completed : BuildStageState.Failed));
            return await CompleteAsync(
                verification.Success,
                outputPath,
                workspace,
                processResults,
                null,
                options.Target,
                options.CleanupPolicy,
                verification,
                toolchainVersions,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await HandleCancellationAsync(workspace, options.CleanupPolicy);
            throw;
        }
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

    private static Dictionary<string, string?> CreateToolEnvironment(
        ResolvedBuildEnvironment toolchain) =>
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["INCLUDE"] = string.Join(Path.PathSeparator, toolchain.IncludePaths),
            ["LIB"] = string.Join(Path.PathSeparator, toolchain.LibraryPaths)
        };

    private static ProcessRequest CreateResourceRequest(
        ResolvedBuildEnvironment toolchain,
        BuildWorkspace workspace,
        string resourcePath)
    {
        var arguments = new List<string>
        {
            "/nologo",
            $"/fo{resourcePath}"
        };
        arguments.AddRange(toolchain.IncludePaths.Select(path => $"/i{path}"));
        arguments.Add(Path.Combine(workspace.GeneratedDirectory, "keyboard.rc"));
        return new ProcessRequest(
            toolchain.ResourceCompilerPath,
            arguments,
            workspace.RootDirectory,
            CreateToolEnvironment(toolchain));
    }

    private static ProcessRequest CreateLinkRequest(
        ResolvedBuildEnvironment toolchain,
        BuildWorkspace workspace,
        string objectPath,
        string resourcePath,
        string outputPath)
    {
        var arguments = new List<string>
        {
            "/NOLOGO",
            "/DLL",
            "/NOENTRY",
            toolchain.Target == BuildTarget.WindowsX64 ? "/MACHINE:X64" : "/MACHINE:ARM64",
            $"/DEF:{Path.Combine(workspace.GeneratedDirectory, "keyboard.def")}",
            $"/OUT:{outputPath}",
            $"/PDB:{Path.ChangeExtension(outputPath, ".pdb")}",
            "/OPT:REF",
            "/OPT:ICF",
            "/Brepro",
            objectPath,
            resourcePath
        };
        arguments.AddRange(toolchain.LibraryPaths.Select(path => $"/LIBPATH:{path}"));
        return new ProcessRequest(
            toolchain.LinkerPath,
            arguments,
            workspace.RootDirectory,
            CreateToolEnvironment(toolchain));
    }

    private static void ValidateOutputFileName(string outputFileName)
    {
        if (string.IsNullOrWhiteSpace(outputFileName) ||
            Path.IsPathRooted(outputFileName) ||
            !string.Equals(outputFileName, Path.GetFileName(outputFileName), StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(outputFileName), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The native output name must be a leaf file name with a .dll extension.",
                nameof(outputFileName));
        }
    }

    private static string SelectToolOutput(ProcessResult result)
    {
        var output = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return string.IsNullOrWhiteSpace(output)
            ? $"{Path.GetFileName(result.Executable)} exited with code {result.ExitCode}."
            : output.Trim();
    }

    private static async Task<CompilationResult> CompleteAsync(
        bool success,
        string? artifactPath,
        BuildWorkspace workspace,
        IReadOnlyList<ProcessResult> processResults,
        string? fallbackCode,
        BuildTarget target,
        BuildCleanupPolicy cleanupPolicy,
        ArtifactVerificationResult? verification,
        BuildToolchainVersions toolchainVersions,
        CancellationToken cancellationToken)
    {
        var messages = MsvcCompilerMessageParser.Parse(processResults.ToArray()).ToList();
        if (!success &&
            fallbackCode is not null &&
            !messages.Any(message => message.Severity == CompilerMessageSeverity.Error))
        {
            messages.Add(new CompilerMessage(fallbackCode, SelectToolOutput(processResults[^1])));
        }

        if (verification is not null)
        {
            messages.AddRange(verification.Messages);
        }

        var rawLog = CreateRawLog(processResults);
        var logPath = Path.Combine(workspace.LogsDirectory, "build.log");
        await File.WriteAllTextAsync(logPath, rawLog, cancellationToken);
        await WriteDiagnosticFilesAsync(
            success,
            target,
            workspace,
            processResults,
            toolchainVersions,
            cancellationToken);
        var retainedLogPath = logPath;
        var retainedWorkspacePath = workspace.RootDirectory;
        if (success && cleanupPolicy != BuildCleanupPolicy.KeepAll)
        {
            workspace.DeleteIntermediates();
        }
        else if (!success && cleanupPolicy == BuildCleanupPolicy.DeleteFailedBuild)
        {
            workspace.Delete();
            retainedLogPath = null;
            retainedWorkspacePath = null;
        }

        return new CompilationResult(
            success,
            artifactPath,
            messages,
            rawLog,
            retainedLogPath,
            retainedWorkspacePath,
            verification,
            toolchainVersions);
    }

    private static async Task WriteDiagnosticFilesAsync(
        bool success,
        BuildTarget target,
        BuildWorkspace workspace,
        IReadOnlyList<ProcessResult> processResults,
        BuildToolchainVersions toolchainVersions,
        CancellationToken cancellationToken)
    {
        var logFileNames = new[] { "compiler.log", "resource-compiler.log", "linker.log" };
        for (var index = 0; index < processResults.Count; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(workspace.LogsDirectory, logFileNames[index]),
                CreateRawLog([processResults[index]]),
                cancellationToken);
        }

        var generatedSources = Directory.EnumerateFiles(workspace.GeneratedDirectory)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var processes = processResults.Select((result, index) => new
        {
            stage = index switch
            {
                0 => "compile",
                1 => "resource",
                _ => "link"
            },
            result.Executable,
            result.Arguments,
            result.ExitCode,
            durationMilliseconds = result.Duration.TotalMilliseconds,
            logFile = logFileNames[index]
        });
        var manifest = new
        {
            schemaVersion = 1,
            target = target.ToString(),
            success,
            generatedSources,
            toolchain = toolchainVersions,
            processes
        };
        var json = JsonSerializer.Serialize(manifest, DiagnosticJsonOptions) + "\n";
        await File.WriteAllTextAsync(
            Path.Combine(workspace.LogsDirectory, "native-build-diagnostics.json"),
            json,
            new UTF8Encoding(false),
            cancellationToken);
    }

    private static async Task HandleCancellationAsync(
        BuildWorkspace workspace,
        BuildCleanupPolicy cleanupPolicy)
    {
        var logPath = Path.Combine(workspace.LogsDirectory, "cancellation.log");
        await File.WriteAllTextAsync(
            logPath,
            "Build cancelled. The active child process was terminated.\n",
            CancellationToken.None);
        if (cleanupPolicy == BuildCleanupPolicy.DeleteFailedBuild)
        {
            workspace.Delete();
        }
    }

    private static string CreateRawLog(IEnumerable<ProcessResult> results)
    {
        var builder = new StringBuilder();
        foreach (var result in results)
        {
            builder.Append("$ ")
                .Append(result.Executable);
            foreach (var argument in result.Arguments)
            {
                builder.Append(' ')
                    .Append(FormatArgument(argument));
            }

            builder.AppendLine();
            if (!string.IsNullOrEmpty(result.StandardOutput))
            {
                builder.AppendLine("[stdout]")
                    .AppendLine(result.StandardOutput.TrimEnd());
            }

            if (!string.IsNullOrEmpty(result.StandardError))
            {
                builder.AppendLine("[stderr]")
                    .AppendLine(result.StandardError.TrimEnd());
            }

            builder.Append("[exit ")
                .Append(result.ExitCode.ToString(CultureInfo.InvariantCulture))
                .Append(", duration ")
                .Append(result.Duration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture))
                .AppendLine(" ms]");
        }

        return builder.ToString();
    }

    private static string FormatArgument(string argument) =>
        argument.Any(char.IsWhiteSpace)
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
}
