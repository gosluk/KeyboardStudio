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
