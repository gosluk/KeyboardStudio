# KeyboardStudio Architecture

## 1. Purpose

KeyboardStudio is a cross-platform keyboard-layout editor written with Avalonia. The editor owns a
platform-neutral keyboard project model. Platform backends translate that model into either a native
Windows keyboard-layout DLL or a Linux XKB symbols component.

The first version focuses on four capabilities:

1. displaying a physical keyboard;
2. editing key mappings;
3. saving and loading a project;
4. selecting an artifact target and producing a Windows DLL or Linux XKB layout file.

Everything not required by those capabilities is intentionally excluded from the first implementation.

---

## 2. Architectural principles

### 2.1 Platform-neutral core

`KeyboardStudio.Core` must not reference:

- Avalonia;
- Windows APIs;
- Windows SDK or WDK types;
- MSVC;
- XKB key names, keysyms, or libxkbcommon types;
- filesystem UI abstractions;
- installer or registry APIs.

The core represents what a keyboard layout means, not how a specific operating system implements it.

### 2.2 Platform translation at the boundary

Windows structures such as scan-code mappings, virtual keys, modifier tables and `KBDTABLES` are generated only in `KeyboardStudio.Windows`.
XKB symbolic key names, keysyms, levels, types, and symbols-component syntax are generated only in
the planned `KeyboardStudio.Linux` backend.

The editor thinks in terms of:

```text
Physical key + modifier layer -> output
```

The selected backend translates that model to its target representation.

### 2.3 Generation, materialization, and compilation are separate

Artifact generation is deterministic and tool-independent. A target backend then materializes or
compiles those generated files according to the target's actual delivery format.

```text
                         KeyboardProject
                                |
                                v
                      Select one BuildTarget
                         /              \
                        v                v
             Windows backend       Linux XKB backend
             translate to          translate to
             Windows model         XKB model
                   |                    |
                   v                    v
             generate C/.def/.rc   generate symbols text
                   |                    |
                   v                    v
             compile and link      write final artifact
                   |                    |
                   v                    v
             verify PE/export      verify with xkbcli
                   |                    |
                   v                    v
               <id>.dll          symbols/<layout-id>
```

This keeps both generators deterministic and unit-testable without requiring MSVC or `xkbcli`.
`INativeCompiler` remains a Windows-backend collaborator; Linux does not use a fake compiler stage.

The Phase 6 generator emits the WDK-native `VSC_VK`, `VSC_LPWSTR`, `VK_TO_BIT`, `MODIFIERS`,
`VK_TO_WCHARS<n>`, `VK_TO_WCHAR_TABLE`, and `KBDTABLES` structures. The exported
`KbdLayerDescriptor` returns that descriptor, while `.def` and `.rc` companions provide the DLL
export and deterministic version metadata. See
[`WINDOWS-KBDTABLES-REFERENCE.md`](WINDOWS-KBDTABLES-REFERENCE.md) for the supported ABI subset.

The planned Linux generator emits classic XKB text format v1 as an `xkb_symbols` component. The
component composes with normal host keycodes/types/compat data, making it more portable than a
self-contained keymap. See [`LINUX-XKB.md`](LINUX-XKB.md).

### 2.4 All project mutations flow through the editor service

ViewModels should not directly mutate arbitrary nested domain objects. Editing operations are concentrated behind `KeyboardEditor` so validation, dirty tracking and future undo/redo can be introduced without redesigning the UI.

### 2.5 One build invocation selects one target backend

The same project can be built repeatedly for different targets, but one invocation resolves exactly
one backend from `BuildOptions.Target`. Common validation runs before dispatch; target validation and
artifact stages run only inside the selected backend. This prevents an unavailable Windows toolchain
from blocking XKB generation and prevents Linux tools from affecting Windows builds.

---

## 3. Solution structure

```text
KeyboardStudio.sln

src/
  KeyboardStudio.App/
  KeyboardStudio.Core/
  KeyboardStudio.Persistence/
  KeyboardStudio.Windows/
  KeyboardStudio.Linux/
  KeyboardStudio.Build/

tests/
  KeyboardStudio.Core.Tests/
  KeyboardStudio.Windows.Tests/
  KeyboardStudio.Linux.Tests/
  KeyboardStudio.App.Tests/

templates/

docs/
```

### Dependency direction

```text
KeyboardStudio.App (composition root)
 |- KeyboardStudio.Core
 |- KeyboardStudio.Persistence -> Core
 |- KeyboardStudio.Build       -> Core
 |- KeyboardStudio.Windows     -> Build + Core
 `- KeyboardStudio.Linux       -> Build + Core
```

The application is the composition root and may reference concrete backends to register them. Its
ViewModels depend on build abstractions, not Windows or Linux generator types. Platform and UI
concerns point inward; Core never references them.

---

## 4. Core domain model

The aggregate root is `KeyboardProject`.

```csharp
public sealed class KeyboardProject
{
    public required ProjectMetadata Metadata { get; init; }
    public required PhysicalKeyboard Keyboard { get; init; }
    public required KeyboardLayout Layout { get; init; }
}
```

Conceptually:

```text
KeyboardProject
 |- Metadata
 |   |- Name
 |   |- Description
 |   |- Version
 |   `- Language
 |
 |- PhysicalKeyboard
 |   `- Keys[]
 |
 `- KeyboardLayout
     `- Mappings[]
```

### 4.1 Physical keyboard

The physical keyboard describes geometry and physical identity.

```csharp
public sealed class PhysicalKeyboard
{
    public required string Id { get; init; }
    public required IReadOnlyList<PhysicalKey> Keys { get; init; }
}

public sealed class PhysicalKey
{
    public required string Id { get; init; }
    public required int ScanCode { get; init; }
    public bool Extended { get; init; }

    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 1;
    public double Height { get; init; } = 1;
}
```

Coordinates are keyboard units rather than pixels. A normal key has width `1.0`; wider keys use values such as `1.5`, `1.75`, `2.25`, or `6.25`.

### 4.2 Physical identity is separate from output

A physical key must not contain a mutable `Character` property. Physical identity and logical mapping are separate.

```text
Physical key
    |
    v
Logical key
    |
    +-- Default       -> a
    +-- Shift         -> A
    +-- AltGr         -> ą
    `-- Shift + AltGr -> Ą
```

The physical keyboard stays constant while mappings vary between projects.

### 4.3 Modifier layers

The initial modifier model is deliberately small.

```csharp
public enum ModifierLayer
{
    Default,
    Shift,
    AltGr,
    ShiftAltGr
}
```

Additional Windows modifier combinations can be added later without exposing Windows-specific modifier bits to the editor.

### 4.4 Key mappings

```csharp
public sealed class KeyMapping
{
    public required string KeyId { get; init; }
    public LogicalKey LogicalKey { get; set; }
    public Dictionary<ModifierLayer, KeyOutput> Outputs { get; init; } = new();
}

public abstract record KeyOutput;
public sealed record CharacterOutput(string Value) : KeyOutput;
public sealed record SpecialKeyOutput(LogicalKey Key) : KeyOutput;
public sealed record NoOutput : KeyOutput;
```

Example:

```csharp
new KeyMapping
{
    KeyId = "KeyA",
    LogicalKey = LogicalKey.A,
    Outputs =
    {
        [ModifierLayer.Default] = new CharacterOutput("a"),
        [ModifierLayer.Shift] = new CharacterOutput("A"),
        [ModifierLayer.AltGr] = new CharacterOutput("ą"),
        [ModifierLayer.ShiftAltGr] = new CharacterOutput("Ą")
    }
};
```

---

## 5. Editing service

All domain mutations pass through one service.

```csharp
public sealed class KeyboardEditor
{
    public KeyboardProject Project { get; }

    public void MapCharacter(
        string keyId,
        ModifierLayer layer,
        string character);

    public void MapLogicalKey(
        string keyId,
        LogicalKey key);

    public void ClearMapping(
        string keyId,
        ModifierLayer layer);
}
```

Benefits:

- unit-testable editing behavior;
- one place for invariants;
- future undo/redo can wrap operations as reversible commands;
- ViewModels remain presentation/orchestration objects rather than domain services.

Undo/redo itself is not part of the initial implementation.

---

## 6. Avalonia application architecture

The initial shell contains three functional areas.

```text
MainWindow
 |- KeyboardEditorView
 |- KeyMappingView
 `- BuildView
```

### 6.1 ViewModels

```text
MainWindowViewModel
 |
 |- KeyboardEditorViewModel
 |   |- Keys
 |   |- SelectedKey
 |   `- ActiveModifier
 |
 |- KeyMappingViewModel
 |   `- Selected key mapping fields
 |
 `- BuildViewModel
     |- SelectedTarget
     |- TargetProfile
     |- EnvironmentStatus
     |- BuildCommand
     |- Stages
     `- BuildResult
```

ViewModels must not depend on Windows- or XKB-specific generator classes. Concrete backends are
registered at the application composition root and reached through `ITargetBuildService`. The build
panel keeps one editable profile per target so switching targets never discards the other target's
settings.

### 6.2 Keyboard rendering

Do not hand-code a button for every key in XAML. Render keys from the physical keyboard model.

```text
KeyboardEditorView
       |
       v
ItemsControl
       |
       v
Canvas / positioned panel
       |
       v
KeyControl x N
```

Conceptual markup:

```xml
<ItemsControl ItemsSource="{Binding Keys}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <Canvas />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

### 6.3 Reusable KeyControl

The key control is intentionally dumb.

```text
KeyControl
 |- KeyId
 |- Label
 |- Width
 |- Height
 |- IsSelected
 `- SelectCommand
```

It does not know about scan-code tables or native compilation.

### 6.4 Modifier layer UI

The editor exposes four layer selectors:

```text
[Normal] [Shift] [AltGr] [Shift + AltGr]
```

Changing the active layer changes the character shown on every mapped key.

### 6.5 Editing flow

```text
User selects key
      |
      v
KeyboardEditorViewModel.SelectedKey
      |
      v
KeyMappingViewModel loads mapping
      |
      v
User changes output
      |
      v
KeyboardEditor.MapCharacter(...)
      |
      v
KeyboardProject updated
```

---

## 7. Persistence

Projects are stored as JSON with the `.kbdproj` extension.

```csharp
public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination);
    Task<KeyboardProject> LoadAsync(Stream source);
}
```

Initial implementation:

```text
KeyboardStudio.Persistence
 `- JsonKeyboardProjectStore
```

Use `System.Text.Json`.

Every project must contain `schemaVersion` from the first release so migrations can be added later. See [PROJECT-FORMAT.md](PROJECT-FORMAT.md).

`KeyboardProject` remains the platform-neutral aggregate. The application/document boundary adds
boundary for optional target profiles such as `WindowsLayoutMetadata` and `XkbLayoutMetadata`.
Profiles are persisted with stable target discriminators without adding their fields to
`ProjectMetadata` or making Core reference a platform backend. One project may retain profiles for
both targets; `BuildOptions.Target` selects which one is consumed.

---

## 8. Validation

Validation occurs before target translation/build.

```csharp
public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    string? KeyId);
```

Initial validation rules:

- physical key IDs are unique;
- scan-code definitions are valid;
- duplicate physical scan-code mappings are rejected;
- output values are valid for the supported output type;
- required logical mappings are valid;
- target-independent mappings are internally consistent.

After common validation, only the selected backend runs its compatibility rules:

- Windows metadata, logical-key support, scan codes, and generated identifiers;
- Linux XKB metadata, template-key coverage, keysym support, and generated identifiers.

A validation issue may include `KeyId` so the UI can highlight the problematic key.

---

## 9. Windows backend

The Windows backend translates the platform-neutral model into an internal Windows model.

```text
KeyboardProject
      |
      v
WindowsLayoutTranslator
      |
      v
WindowsKeyboardLayout
      |
      v
WindowsCSourceGenerator
```

Windows semantic model:

```csharp
public sealed record WindowsKeyboardLayout
{
    public required IReadOnlyList<VscToVkMapping> VscToVkMappings { get; init; }
    public required IReadOnlyList<ExtendedVscToVkMapping> ExtendedVscToVkMappings { get; init; }
    public required WindowsModifierTable Modifiers { get; init; }
    public required WindowsCharacterTable Characters { get; init; }
}
```

The model has an explicit `LogicalKey` to `WindowsVirtualKey` translation, distinct normal and extended
scan-code collections, Windows Ctrl+Alt semantics for AltGr, and typed character rows. Scan-only keys
are represented only in scan-code tables. Translation failures carry structured, key-linked
diagnostics. The Windows model is never exposed to Avalonia or serialized into `.kbdproj`.

The generated source ultimately describes the native Windows keyboard tables and exposes the keyboard-table descriptor expected by Windows. See [WINDOWS-BUILD.md](WINDOWS-BUILD.md).

After linking, the Windows path parses the PE headers and named export directory on every host. It
requires the selected x64/ARM64 machine, the DLL characteristic, and the exact undecorated
`KbdLayerDescriptor` export. A matching-architecture Windows process additionally loads the module,
resolves the export, and frees it without registering or installing the layout. Only then does
orchestration write the versioned source/toolchain/artifact manifest. An opt-in reproducibility run
generates and compiles twice, comparing source exactly and DLLs by SHA-256.

---

## 10. Linux XKB backend

The Linux backend translates the platform-neutral model into a typed XKB symbols model before
emitting text.

```text
KeyboardProject
      |
      v
XkbLayoutTranslator
      |
      v
XkbKeyboardLayout
      |
      v
XkbSymbolsGenerator
      |
      v
symbols/<layout-id>
```

The intermediate model carries a sanitized layout/section identity and ordered mappings with an XKB
key name, optional key type, and up to four keysyms. Core layers map to XKB levels 1-4:

| Core layer | XKB level | Meaning |
|---|---:|---|
| `Default` | 1 | no modifier |
| `Shift` | 2 | Shift |
| `AltGr` | 3 | LevelThree |
| `ShiftAltGr` | 4 | Shift+LevelThree |

### 10.1 Physical key translation

The common identity is `(PhysicalKeyboard.Id, PhysicalKey.Id)`. The Linux backend maps that pair to
standard XKB key names such as `<AE01>`, `<AC01>`, `<LSGT>`, and `<KPEN>`. It must not infer XKB
identity from `PhysicalKey.ScanCode`: that field currently supports the Windows set-1 translation and
does not encode the XKB key-name convention.

Explicit ISO-105 and ANSI-104 maps make international and keypad differences reviewable. Missing
pairs produce key-linked target diagnostics.

### 10.2 Symbols format

The final artifact is a classic XKB text format v1 `xkb_symbols` component stored as
`symbols/<layout-id>`. V1 is selected for interoperability with both X11 tooling and Wayland clients.
The generator uses canonical keysym names for known special keys and deterministic Unicode `U...`
notation for character outputs. `NoSymbol` fills missing intermediate levels.

A component file is preferable to a self-contained `xkb_keymap` because it composes with the host's
standard keycodes, types, compatibility data, and rules. The generator does not copy or modify the
system XKB database. Automatic installation and activation are outside the build boundary. See
[`LINUX-XKB.md`](LINUX-XKB.md).

### 10.3 Verification

Managed validation always checks identifiers, key coverage, keysyms, and output structure. When
available, `xkbcli compile-keymap` verifies the generated component in an isolated include root
combined with the system defaults. Versions 1.9 and newer add `--test`; older versions compile and
discard the emitted full keymap. The verifier is mandatory in Linux integration CI but optional for
local generation, so XKB text can be produced on any supported host.

---

## 11. Build orchestration

### 11.1 Target dispatch

The former fixed `IArtifactGenerator` + `IBuildEnvironment` + `INativeCompiler` constructor modeled
only the Windows pipeline. Those collaborators now live behind a target backend:

```csharp
public interface IBuildBackend
{
    IReadOnlySet<BuildTarget> SupportedTargets { get; }
    BuildEnvironmentStatus GetStatus(BuildTarget target);
    Task<KeyboardBuildResult> BuildAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default);
}
```

An `IBuildBackendResolver` returns exactly one backend for `BuildOptions.Target`. `BuildOrchestrator`
runs Core validation first and invokes no backend if common errors exist. The selected backend then
runs compatibility validation and reports its own stages.

| Target | Backend path | Required tools | Verification | Final artifact |
|---|---|---|---|---|
| `WindowsX64` | C generation -> compile -> link | MSVC + Windows SDK/WDK | PE/export verifier | `<layout-id>.dll` |
| `WindowsArm64` | C generation -> compile -> link | MSVC + Windows SDK/WDK | PE/export verifier | `<layout-id>.dll` |
| `LinuxXkb` | symbols generation -> write | none | `xkbcli` when available; required in CI | `symbols/<layout-id>` |

This is single-target dispatch, not host dispatch. A Linux XKB artifact may be generated on Windows or
macOS because it is deterministic text. A Windows DLL requires the supported Windows toolchain.

### 11.2 Target profiles

`BuildOptions.Target` chooses the output kind. The associated profile supplies backend metadata:

```text
WindowsX64 / WindowsArm64 -> WindowsLayoutMetadata
LinuxXkb                  -> XkbLayoutMetadata
```

Profiles belong to the application/project-document boundary, remain separate from Core metadata,
and may coexist for one project. Changing the target does not mutate key mappings.

### 11.3 Windows build collaborators

The Windows backend retains the existing abstractions internally:

```csharp
public interface IBuildEnvironment
{
    bool CanBuild(BuildTarget target);
    BuildEnvironmentStatus GetStatus(BuildTarget target);
    ResolvedBuildEnvironment? Resolve(BuildTarget target);
}

public interface IArtifactGenerator
{
    Task<GeneratedArtifact> GenerateAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default);
}

public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedArtifact artifact,
        BuildOptions options,
        CancellationToken cancellationToken);
}
```

Windows environment resolution prefers an active developer environment and supported Visual
Studio/Windows Kits discovery. Compilation uses a unique workspace, argument-list process execution,
parsed diagnostics, and a retained raw log.

### 11.4 Target-neutral results and stages

At the orchestration/UI boundary, result names describe an artifact rather than assuming compilation.
`KeyboardBuildResult.Artifact` contains:

```text
Success and artifact path
Common and target diagnostics
Raw/verifier log and retained log path
Manifest and workspace paths
Artifact SHA-256
Typed backend details
```

Compiler messages remain in a detailed Windows `CompilationResult` below the backend and are exposed
through a compatibility accessor. XKB verifier messages map to the target-neutral diagnostic envelope
without being mislabeled as compiler output.

---

## 12. Keyboard templates

Physical geometry is supplied as reusable templates rather than duplicated into every project.

```text
templates/
 |- ansi-104.json
 |- iso-105.json
 `- jis-109.json
```

The first implementation prioritizes ISO-105 and ANSI-104. A project stores the template identifier;
mappings store project-specific behavior. Platform backends own their mappings from stable template
key IDs to native physical identities.

---

## 13. Testing strategy

### KeyboardStudio.Core.Tests

Test:

- mapping operations;
- modifier-layer behavior;
- project invariants;
- validation;
- template/domain transformations;
- persistence-independent editing behavior.

### KeyboardStudio.Windows.Tests

Test:

- scan-code to virtual-key translation;
- modifier translation;
- generated character tables;
- Unicode output mappings;
- deterministic source generation;
- representative golden/source snapshots;
- native compilation and PE/export verification in Windows integration tests.

### KeyboardStudio.Linux.Tests

Test:

- template key ID to XKB key-name translation;
- logical/special key to keysym translation;
- two- and four-level modifier behavior;
- Unicode keysym generation;
- deterministic symbols-component golden files;
- `xkbcli` verification in Linux integration tests.

### KeyboardStudio.App.Tests

Test target selection, backend resolution, target-specific command enablement, dynamic stage
presentation, cancellation, and result/error presentation without referencing concrete generators.

---

## 14. Initial MVP boundary

### Included

- display ISO/ANSI physical keyboard;
- select keys;
- map physical key to logical key;
- map `Default`, `Shift`, `AltGr`, and `ShiftAltGr`;
- Unicode character outputs;
- save/load `.kbdproj` and target profiles;
- common and target-specific validation;
- native Windows source generation and DLL compilation;
- deterministic Linux XKB v1 symbols generation;
- structural/tool verification for both artifact paths.

### Excluded

- Windows installation and registry registration;
- automatic XKB installation, desktop registration, or activation;
- dead keys;
- chained dead keys;
- ligatures;
- macros;
- arbitrary scripts;
- IMEs;
- runtime hooks/remapping;
- importing existing native artifacts.

The domain model should remain extensible enough to add these later without complicating the first implementation.

---

## 15. Critical abstractions

```text
IKeyboardProjectStore
IKeyboardProjectValidator
IBuildBackendResolver
IBuildBackend
IArtifactGenerator             (backend-internal generation)
INativeCompiler                (Windows backend only)
```

These boundaries protect the editor from persistence and platform-specific implementation details.

---

## 16. Architectural summary

```text
KeyboardStudio.App (composition + target-aware UI)
 |
 +--> KeyboardStudio.Persistence --> KeyboardStudio.Core
 |
 +--> KeyboardStudio.Build --> common validation --> backend resolver
 |                                               /          \
 |                                              v            v
 |                              KeyboardStudio.Windows   KeyboardStudio.Linux
 |                              C/.def/.rc generation    XKB symbols generation
 |                              MSVC compile/link        artifact writer
 |                              PE/export verification  xkbcli verification
 |                                      |                     |
 |                                      v                     v
 |                                  <id>.dll          symbols/<layout-id>
 |
 `--> KeyboardStudio.Core
      KeyboardProject -> PhysicalKeyboard + KeyboardLayout + ProjectMetadata
```
