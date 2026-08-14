# KeyboardStudio Architecture

## 1. Purpose

KeyboardStudio is a cross-platform keyboard-layout editor written with Avalonia. The editor owns a platform-neutral keyboard project model. Windows-specific keyboard layout structures exist only in the Windows compilation backend.

The first version focuses on four capabilities:

1. displaying a physical keyboard;
2. editing key mappings;
3. saving and loading a project;
4. compiling a native Windows keyboard-layout artifact.

Everything not required by those capabilities is intentionally excluded from the first implementation.

---

## 2. Architectural principles

### 2.1 Platform-neutral core

`KeyboardStudio.Core` must not reference:

- Avalonia;
- Windows APIs;
- Windows SDK or WDK types;
- MSVC;
- filesystem UI abstractions;
- installer or registry APIs.

The core represents what a keyboard layout means, not how a specific operating system implements it.

### 2.2 Windows translation at the boundary

Windows structures such as scan-code mappings, virtual keys, modifier tables and `KBDTABLES` are generated only in `KeyboardStudio.Windows`.

The editor thinks in terms of:

```text
Physical key + modifier layer -> output
```

The Windows backend translates that model to the native Windows representation.

### 2.3 Generation and compilation are separate

Generating native C source and invoking a native compiler are distinct responsibilities.

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
      |
      v
Generated source files
      |
      v
INativeCompiler
      |
      v
Keyboard layout DLL
```

This keeps source generation deterministic and unit-testable without requiring a Windows compiler installation.

The Phase 6 generator emits the WDK-native `VSC_VK`, `VSC_LPWSTR`, `VK_TO_BIT`, `MODIFIERS`,
`VK_TO_WCHARS<n>`, `VK_TO_WCHAR_TABLE`, and `KBDTABLES` structures. The exported
`KbdLayerDescriptor` returns that descriptor, while `.def` and `.rc` companions provide the DLL
export and deterministic version metadata. See
[`WINDOWS-KBDTABLES-REFERENCE.md`](WINDOWS-KBDTABLES-REFERENCE.md) for the supported ABI subset.

### 2.4 All project mutations flow through the editor service

ViewModels should not directly mutate arbitrary nested domain objects. Editing operations are concentrated behind `KeyboardEditor` so validation, dirty tracking and future undo/redo can be introduced without redesigning the UI.

---

## 3. Solution structure

```text
KeyboardStudio.sln

src/
  KeyboardStudio.App/
  KeyboardStudio.Core/
  KeyboardStudio.Persistence/
  KeyboardStudio.Windows/
  KeyboardStudio.Build/

tests/
  KeyboardStudio.Core.Tests/
  KeyboardStudio.Windows.Tests/

templates/

docs/
```

### Dependency direction

```text
                         +----------------------+
                         | KeyboardStudio.App   |
                         +----------+-----------+
                                    |
                 +------------------+------------------+
                 |                                     |
                 v                                     v
       +----------------------+            +---------------------------+
       | KeyboardStudio.Core  |<-----------| KeyboardStudio.Persistence|
       +----------+-----------+            +---------------------------+
                  ^
                  |
       +----------+-----------+
       | KeyboardStudio.Windows|
       +----------+-----------+
                  ^
                  |
       +----------+-----------+
       | KeyboardStudio.Build |
       +----------------------+
```

The exact project-reference graph can use dependency inversion, but the rule remains: UI and platform concerns point inward, never the reverse.

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
     |- TargetArchitecture
     |- BuildCommand
     `- BuildResult
```

ViewModels must not depend on Windows-specific classes.

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

---

## 8. Validation

Validation occurs before native translation/build.

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
- Windows metadata is valid;
- generated Windows identifiers are valid.

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

---

## 10. Build orchestration

```text
Build command
    |
    v
Validate project
    |
    v
Translate to Windows model
    |
    v
Generate C/native source
    |
    v
Invoke native compiler
    |
    v
Validate build result
    |
    v
Output keyboard-layout DLL
```

The editor may run on Windows, Linux, and macOS. Native Windows compilation is enabled only when the required Windows toolchain is available.

### 10.1 Build environment abstraction

```csharp
public interface IBuildEnvironment
{
    bool CanBuild(BuildTarget target);
    BuildEnvironmentStatus GetStatus();
}
```

### 10.2 Artifact generation abstraction

```csharp
public interface IArtifactGenerator
{
    Task<GeneratedArtifact> GenerateAsync(
        KeyboardProject project,
        BuildOptions options,
        CancellationToken cancellationToken = default);
}
```

### 10.3 Native compiler abstraction

```csharp
public interface INativeCompiler
{
    Task<CompilationResult> CompileAsync(
        GeneratedSource source,
        BuildTarget target,
        CancellationToken cancellationToken);
}
```

Generation remains testable without invoking the toolchain.

---

## 11. Keyboard templates

Physical geometry is supplied as reusable templates rather than duplicated into every project.

```text
templates/
 |- ansi-104.json
 |- iso-105.json
 `- jis-109.json
```

The first implementation prioritizes ISO-105 and ANSI-104. A project stores the template identifier; mappings store project-specific behavior.

---

## 12. Testing strategy

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
- representative golden/source snapshots.

Native compiler invocation belongs in separate Windows-only integration tests when implementation begins.

---

## 13. Initial MVP boundary

### Included

- display ISO/ANSI physical keyboard;
- select keys;
- map physical key to logical key;
- map `Default`, `Shift`, `AltGr`, and `ShiftAltGr`;
- Unicode character outputs;
- save/load `.kbdproj`;
- project validation;
- native Windows source generation;
- Windows DLL compilation.

### Excluded

- installation and registry registration;
- dead keys;
- chained dead keys;
- ligatures;
- macros;
- arbitrary scripts;
- IMEs;
- runtime hooks/remapping;
- importing existing native DLLs.

The domain model should remain extensible enough to add these later without complicating the first implementation.

---

## 14. Critical abstractions

Establish these interfaces early:

```text
IKeyboardProjectStore
IKeyboardProjectValidator
IArtifactGenerator
INativeCompiler
```

These boundaries protect the editor from persistence and platform-specific implementation details.

---

## 15. Architectural summary

```text
                         KeyboardStudio

+-----------------------------------------------------------+
| KeyboardStudio.App                                        |
|                                                           |
| MainWindow                                                |
|  |- KeyboardEditorView -> KeyControl x N                  |
|  |- KeyMappingView                                        |
|  `- BuildView                                             |
|                                                           |
| ViewModels                                                |
+--------------------------+--------------------------------+
                           |
                           v
+-----------------------------------------------------------+
| KeyboardStudio.Core                                       |
|                                                           |
| KeyboardProject                                           |
|  |- ProjectMetadata                                       |
|  |- PhysicalKeyboard -> PhysicalKey                       |
|  `- KeyboardLayout -> KeyMapping                          |
|                      `- ModifierLayer -> KeyOutput         |
|                                                           |
| KeyboardEditor                                            |
| KeyboardProjectValidator                                  |
+---------------+-----------------------+-------------------+
                |                       |
                v                       v
+--------------------------+  +-----------------------------+
| Persistence              |  | Windows                     |
| JsonKeyboardProjectStore |  | WindowsLayoutTranslator     |
| .kbdproj                 |  | WindowsKeyboardLayout       |
+--------------------------+  | WindowsCSourceGenerator     |
                              +-------------+---------------+
                                            |
                                            v
                              +-----------------------------+
                              | Build                       |
                              | WindowsBuildEnvironment     |
                              | MsvcKeyboardCompiler        |
                              | C -> OBJ -> DLL             |
                              +-----------------------------+
```
