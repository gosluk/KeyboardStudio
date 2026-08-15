# Windows Build Backend

## Goal

The Windows backend converts a platform-neutral `KeyboardProject` into the native tables required by a Windows keyboard-layout DLL.

KeyboardStudio should not depend on MSKLC or generate `.klc` as its primary build path.

## Pipeline

```text
KeyboardProject
      |
      v
KeyboardProjectValidator
      |
      v
WindowsLayoutTranslator
      |
      v
WindowsKeyboardLayout
      |
      v
WindowsCSourceGenerator
      |
      +-- keyboard.c
      +-- keyboard.h
      +-- keyboard.def
      `-- keyboard.rc   (when required)
      |
      v
INativeCompiler
      |
      v
MSVC / Windows SDK / WDK toolchain
      |
      v
custom keyboard-layout DLL
```

## Translation boundary

The Windows translator converts generic concepts into Windows concepts.

```text
Physical scan code -> Windows virtual key
ModifierLayer      -> Windows modifier state
CharacterOutput    -> native character table entry
```

Windows-specific values must not leak into `KeyboardStudio.Core`.

## Internal Windows model

Phase 6 model:

```csharp
public sealed record WindowsKeyboardLayout
{
    public required IReadOnlyList<VscToVkMapping> VscToVkMappings { get; init; }
    public required IReadOnlyList<ExtendedVscToVkMapping> ExtendedVscToVkMappings { get; init; }
    public required IReadOnlyList<WindowsKeyNameMapping> KeyNames { get; init; }
    public required IReadOnlyList<WindowsKeyNameMapping> ExtendedKeyNames { get; init; }
    public required WindowsModifierTable Modifiers { get; init; }
    public required WindowsCharacterTable Characters { get; init; }
}

public sealed record VscToVkMapping(
    byte ScanCode,
    WindowsVirtualKey VirtualKey);

public sealed record ExtendedVscToVkMapping(
    byte ScanCode,
    WindowsVirtualKey VirtualKey);
```

Character mappings represent the four v1 modifier states. `WindowsCharacterTable.Width` is `2` when
only Default and Shift are populated and `4` when AltGr columns are required.

```csharp
public sealed record WindowsCharacterMapping(
    WindowsVirtualKey VirtualKey,
    WindowsCharacterAttributes Attributes,
    char? Default,
    char? Shift,
    char? AltGr,
    char? ShiftAltGr);
```

AltGr is modeled as Windows Ctrl+Alt and Shift+AltGr as Shift+Ctrl+Alt. The modifier-number table also
contains explicit invalid entries for unsupported Ctrl-only and Alt-only combinations.

Scan-only logical keys never receive a character row. Unsupported character/special-key mappings and
non-BMP characters fail translation with a `WindowsTranslationException` carrying structured
`ValidationIssue` instances; translation never silently drops them.

## Native source generation

`WindowsCSourceGenerator` emits deterministic source from `WindowsKeyboardLayout`.

The generated C translation unit contains dense primary and sentinel-terminated E0/E1 scan tables,
normal and extended key names, native modifier and character tables, a complete MVP `KBDTABLES`,
and the typed `KbdLayerDescriptor` entry point. `keyboard.def` exports that entry at ordinal 1;
`keyboard.rc` contains stable DLL version metadata; and `keyboard.h` fixes `KBD_TYPE` and declares
the descriptor.

The generic `keyboard.*` names are deliberate: each build already has its own project working
directory, while the layout ID determines module/resource identity and the eventual DLL name.
Dead keys, ligatures, and locale-specific optional tables are emitted as ABI-defined null/zero
fields because those features are outside the MVP semantic model.

Identical project input and build options should produce byte-for-byte identical generated source.
Golden fixtures for minimal US, AltGr Unicode, and ISO examples verify all four output files exactly.

## Compiler abstraction

```csharp
public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedArtifact artifact,
        BuildOptions options,
        CancellationToken cancellationToken);
}
```

The compiler implementation owns process execution and compiler-diagnostic parsing.

```csharp
public sealed record CompilationResult(
    bool Success,
    string? ArtifactPath,
    IReadOnlyList<CompilerMessage> Messages,
    string RawLog,
    string? LogPath,
    string? WorkspacePath);
```

## Build environment

The Avalonia editor may run cross-platform. Native Windows compilation is enabled only where the required Windows toolchain is available.

```csharp
public interface IBuildEnvironment
{
    bool CanBuild(BuildTarget target);
    BuildEnvironmentStatus GetStatus(BuildTarget target);
    ResolvedBuildEnvironment? Resolve(BuildTarget target);
}
```

`WindowsBuildEnvironment` detects the host and resolves x64 `cl.exe`, `link.exe`, `rc.exe`, MSVC/SDK
versions, include directories, and library directories. It prefers an active Visual
Studio developer environment, then queries Visual Studio through `vswhere` and Windows Kits through
the registered SDK root. Missing components are returned as structured diagnostics.

The UI should explain why compilation is unavailable instead of failing only after Build is pressed.

## Native command pipeline

The native Windows target is x64. For each build, `MsvcKeyboardCompiler`:

1. invokes `cl.exe` with the resolved headers and architecture define to create `keyboard.obj`;
2. invokes `rc.exe` for deterministic version metadata in `keyboard.rc`;
3. invokes `link.exe /DLL /NOENTRY` with `keyboard.def`, exporting `KbdLayerDescriptor`;
4. writes the layout-ID-derived DLL into `output/`;
5. parses the resulting PE and requires the x64 machine, DLL characteristic, and an
   exact named export for `KbdLayerDescriptor`.

PE and export-directory validation is implemented in managed code, so structural verification does
not depend on `dumpbin` and can be unit-tested on non-Windows hosts.

On Windows, a matching-architecture build also performs a load-level smoke test through
`NativeLibrary.Load` and resolves `KbdLayerDescriptor` before immediately freeing the module. The
helper never installs or registers the layout. Non-Windows hosts record the smoke test as not run;
Windows integration CI is responsible for exercising it.

Processes use `ProcessStartInfo.ArgumentList`, not a composed shell command. Standard output,
standard error, exit code, elapsed duration, executable, arguments, environment, and working
directory are captured. MSVC/RC/link diagnostics are mapped to `CompilerMessage`, while the complete
invocation and output stream is retained in `logs/build.log`.

## Output structure

```text
build/
  build-<unique-id>/
    generated/
      keyboard.c
      keyboard.h
      keyboard.def
      keyboard.rc
    obj/
      keyboard.obj
      keyboard.res
    output/
      <layout>.dll
      build-manifest.json
    logs/
      build.log
      compiler.log
      resource-compiler.log
      linker.log
      native-build-diagnostics.json
```

Generated files are build output and are not part of the `.kbdproj` source model.

After artifact verification, `BuildOrchestrator` writes a versioned JSON manifest beside the DLL. It
records the project name, target, ordered generated-source names and SHA-256 hashes, MSVC and Windows
SDK versions, verified output path and hash, verification state, and the UTC build timestamp. The
timestamp is confined to the manifest and never changes generated source.

Set `BuildOptions.VerifyReproducibility` to build the same project twice. The checker compares the
two generated source dictionaries exactly and the verified DLLs by SHA-256. `link.exe` receives
`/Brepro` so supported MSVC toolchains suppress nondeterministic PE content. A mismatch fails the
overall build with `REPRO_SOURCE` or `REPRO_BINARY`, retains the comparison workspace for diagnosis,
and is recorded in the primary manifest.

Each completed native attempt also writes one log per invoked tool and a structured
`native-build-diagnostics.json` manifest with the target, generated-source inventory, toolchain
versions, commands, exit codes, durations, and log-file names. This intermediate manifest is
available even when compilation or linking fails before the final artifact manifest can be written.
Windows CI retains the entire failed integration workspace for seven days, including generated C,
per-tool logs, and this diagnostic manifest. Successful native intermediates are not uploaded.

Each invocation receives a unique workspace and never compiles in a source directory. With the
default `KeepFailedBuild` policy, successful builds remove `generated/` and `obj/` but retain the DLL
and log; failures and cancellations retain all diagnostic files. `DeleteFailedBuild` removes a
failed/cancelled workspace, while `KeepAll` preserves successful intermediates. Cancellation kills
the child process tree before applying that policy and writes `logs/cancellation.log` when retained.

## References

Microsoft publishes keyboard-layout source examples in the Windows Driver Samples repository:

https://github.com/microsoft/Windows-driver-samples/tree/main/input/layout

Artifact verification follows Microsoft's PE/COFF header and export-directory format and uses the
.NET native-library API for the matching-host loader smoke test:

- https://learn.microsoft.com/windows/win32/debug/pe-format
- https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.nativelibrary
