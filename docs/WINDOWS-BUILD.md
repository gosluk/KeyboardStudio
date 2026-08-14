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

Representative model:

```csharp
internal sealed class WindowsKeyboardLayout
{
    public required IReadOnlyList<VscToVkMapping> ScanCodes { get; init; }
    public required IReadOnlyList<WindowsCharacterMapping> Characters { get; init; }
    public required WindowsModifierTable Modifiers { get; init; }
}

internal sealed record VscToVkMapping(
    byte ScanCode,
    WindowsVirtualKey VirtualKey,
    bool Extended);
```

Character mappings initially represent four modifier states.

```csharp
internal sealed class WindowsCharacterMapping
{
    public required WindowsVirtualKey VirtualKey { get; init; }
    public string? Normal { get; init; }
    public string? Shift { get; init; }
    public string? AltGr { get; init; }
    public string? ShiftAltGr { get; init; }
}
```

## Native source generation

`WindowsCSourceGenerator` emits deterministic source from `WindowsKeyboardLayout`.

The generated code contains the native Windows keyboard-layout tables and exposes the descriptor required by Windows.

Identical project input and build options should produce byte-for-byte identical generated source.

## Compiler abstraction

```csharp
public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedSource source,
        BuildTarget target,
        CancellationToken cancellationToken);
}
```

The compiler implementation owns process execution and compiler-diagnostic parsing.

```csharp
public sealed record CompilationResult(
    bool Success,
    string? ArtifactPath,
    IReadOnlyList<CompilerMessage> Messages);
```

## Build environment

The Avalonia editor may run cross-platform. Native Windows compilation is enabled only where the required Windows toolchain is available.

```csharp
public interface IBuildEnvironment
{
    bool CanBuild(BuildTarget target);
    BuildEnvironmentStatus GetStatus();
}
```

`WindowsBuildEnvironmentDetector` should detect the compiler, SDK/WDK includes/libraries, and linker resources required by the implementation.

The UI should explain why compilation is unavailable instead of failing only after Build is pressed.

## Output structure

```text
build/
  <project-name>/
    generated/
      keyboard.c
      keyboard.h
      keyboard.def
      keyboard.rc
    obj/
    output/
      <layout>.dll
```

Generated files are build output and are not part of the `.kbdproj` source model.

## References

Microsoft publishes keyboard-layout source examples in the Windows Driver Samples repository:

https://github.com/microsoft/Windows-driver-samples/tree/main/input/layout
