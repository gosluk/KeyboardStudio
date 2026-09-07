# KeyboardStudio Architecture

## 1. Purpose

KeyboardStudio is a cross-platform keyboard-layout editor written with Avalonia. The editor owns a
platform-neutral keyboard project model. A platform backend translates that model into a Linux XKB
symbols component.

The first version focuses on five capabilities:

1. displaying a physical keyboard;
2. starting from an existing layout rather than from an empty one;
3. editing key mappings;
4. saving and loading a project;
5. producing a Linux XKB layout file.

Everything not required by those capabilities is intentionally excluded from the first implementation.

The backend, the non-empty/current-layout startup path, the per-user XKB variant workflow, and the
White/Gray/Black appearance and application shell are implemented and tested. Sections 9.4, 12, and
17 describe that current architecture.

---

## 2. Architectural principles

### 2.1 Platform-neutral core

`KeyboardStudio.Core` must not reference:

- Avalonia;
- platform keyboard APIs;
- XKB key names, keysyms, or libxkbcommon types;
- filesystem UI abstractions;
- installer or registry APIs.

The core represents what a keyboard layout means, not how a specific operating system implements it.

### 2.2 Platform translation at the boundary

XKB symbolic key names, keysyms, levels, types, and symbols-component syntax are generated only in
the `KeyboardStudio.Linux` backend. No target-specific structure reaches the core or the editor.

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
                                |
                                v
                        Linux XKB backend
                        translate to
                        XKB model
                                |
                                v
                        generate symbols text
                                |
                                v
                        write final artifact
                                |
                                v
                        verify with xkbcli
                                |
                                v
                        symbols/<layout-id>
```

This keeps generation deterministic and unit-testable without requiring `xkbcli`. Verification is a
separate, optional stage rather than a precondition of generating anything.

The Linux generator emits classic XKB text format v1 as an `xkb_symbols` component. The
component composes with normal host keycodes/types/compat data, making it more portable than a
self-contained keymap. See [`LINUX-XKB.md`](LINUX-XKB.md).

### 2.4 All project mutations flow through the editor service

ViewModels should not directly mutate arbitrary nested domain objects. Editing operations are concentrated behind `KeyboardEditor` so validation, dirty tracking and future undo/redo can be introduced without redesigning the UI.

### 2.5 One build invocation selects one target backend

The same project can be built repeatedly for different targets, but one invocation resolves exactly
one backend from `BuildOptions.Target`. Common validation runs before dispatch; target validation and
artifact stages run only inside the selected backend. This keeps a missing optional verification
tool from blocking generation, and leaves room for a second target without reworking dispatch.

### 2.6 One target today, without collapsing the seam

`BuildTarget` currently has one member, `LinuxXkb`. The dispatch machinery around it —
`BuildOrchestrator`, `IBuildBackendResolver`, `BuildOptions.Target`, per-target profiles — is
deliberately kept rather than inlined.

The seam is what keeps target-specific concerns out of the core and the editor: `IBuildBackend` is
the only place a target's tooling, verification, and artifact shape are allowed to appear. Collapsing
it would push XKB details up into the view models, which section 2.1 forbids for a reason that
outlives any one target.

When exactly one target is registered, the target selector is not rendered at all rather than
rendered with one entry, and the target's identity moves to a badge on the build panel.

---

## 3. Solution structure

```text
KeyboardStudio.slnx

src/
  KeyboardStudio.App/
  KeyboardStudio.Core/
  KeyboardStudio.Persistence/
  KeyboardStudio.Linux/
  KeyboardStudio.Build/

tests/
  KeyboardStudio.Core.Tests/
  KeyboardStudio.Build.Tests/
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
 `- KeyboardStudio.Linux       -> Build + Core
```

The application is the composition root and may reference concrete backends to register them. Its
ViewModels depend on build abstractions, not backend generator types. Platform and UI concerns point
inward; Core never references them.

The same split applies to layout import: `KeyboardStudio.Core/Layouts/Import/` owns the neutral
contract and `KeyboardStudio.Linux/Import/` owns every XKB-specific parser, resolver, and table. See
section 12.

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

Additional modifier combinations can be added later without exposing target-specific modifier bits to the editor.

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
 |- BuildView
 `- ImportLayoutDialog   (modal, opened on demand)
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
 |- BuildViewModel
 |   |- SelectedTarget
 |   |- TargetProfile
 |   |- EnvironmentStatus
 |   |- BuildCommand
 |   |- Stages
 |   `- BuildResult
 |
 `- LayoutImportViewModel        (built per dialog, not held open)
     |- Layouts -> ImportableLayoutViewModel -> ImportableVariantViewModel
     |- SelectedTemplate / UseSuggestedGeometry
     |- CommitMode
     |- PreviewKeys -> KeyViewModel
     `- Report -> LayoutImportReportViewModel
```

ViewModels must not depend on XKB-specific generator classes. Concrete backends are registered at the
application composition root and reached through `ITargetBuildService`. The build panel keeps one
editable profile per target, and renders no target selector while only one target exists (2.6).

Before enabling Build, the service returns one readiness snapshot containing common validation,
selected-target validation, profile/output validation, and environment availability. Common errors
therefore disable every target, while compatibility errors and required-tool failures disable only
the selected target. The asynchronous command also disables itself for the duration of a build.

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


### 6.6 Layout import UI

The host's installed layout catalog is large — 99 layouts and 496 variants on a current
xkeyboard-config — so it is presented in a modal `ImportLayoutDialog` rather than in a dropdown or a
permanent panel. The main window keeps its current proportions.

```text
Entry points
  File > Import layout...
  "Import..." button beside the existing "New from [template] [Create]" control
  File > Import from file...     (an arbitrary symbols file)
        |
        v
ImportLayoutDialog
 |- search box            filters name, layout ID, language, country
 |- catalog list          grouped User / System, layout -> variants
 |- geometry selector     suggested template, user-overridable
 |- preview               read-only KeyboardEditorView over the resolved project
 |- fidelity summary      keys imported, dropped, skipped
 `- [Import as new project] [Replace mappings in current project] [Cancel]
```

The dialog reuses `KeyControl` and the existing geometry rendering for its preview, so an import is
seen before it is committed. Import is lossy (12.4), which makes previewing the fidelity report
before replacement a correctness requirement rather than a nicety.

`LayoutImportViewModel` depends only on `ILayoutImportCatalog`. It must not reference
`KeyboardStudio.Linux` types, exactly as `BuildViewModel` must not reference generator types (6.1).
Both commit paths route through the existing unsaved-changes confirmation, and mapping replacement
goes through `KeyboardEditor` so the future undo/redo boundary (2.4) stays intact.

`HostLayoutImportCatalog` is the composition root for import and the only place in the application
that names a concrete source or a concrete probe. `MainWindowViewModel` takes both as interfaces,
which is what lets every import test but the integration ones run against a fake rather than
whatever the machine happens to have installed.


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

Core-only compatibility implementation:

```text
KeyboardStudio.Persistence
 `- JsonKeyboardProjectStore
```

Use `System.Text.Json`.

Every project must contain `schemaVersion` from the first release so migrations can be added later.
The desktop application persists a `KeyboardProjectDocument` envelope:

```text
KeyboardProjectDocument
 |- documentSchemaVersion
 |- project -> KeyboardProject (Core schemaVersion)
 |- targets
 |   `- linuxXkb   -> XKB build settings
 `- importProvenance   (absent unless the document began as an import)
```

`ProjectDocumentService` owns current path and dirty state. `BuildViewModel` exports the editable
profile through a stable discriminator before a save and reapplies it after open/new. A missing known
profile receives safe defaults; opening a legacy direct Core project remains supported. Profile
discriminators the application does not recognise are ignored on load and are not re-emitted on
save.

See [PROJECT-FORMAT.md](PROJECT-FORMAT.md).

`KeyboardProject` remains the platform-neutral aggregate. The application/document boundary adds a
versioned envelope for optional target profiles such as `XkbLayoutMetadata`. Profiles are persisted
with stable target discriminators without adding their fields to `ProjectMetadata` or making Core
reference a platform backend. `BuildOptions.Target` selects which profile is consumed.

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

- Linux XKB metadata, template-key coverage, keysym support, and generated identifiers.

A validation issue may include `KeyId` so the UI can highlight the problematic key.

---

## 9. Linux XKB backend

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

### 9.1 Physical key translation

The common identity is `(PhysicalKeyboard.Id, PhysicalKey.Id)`. The Linux backend maps that pair to
standard XKB key names such as `<AE01>`, `<AC01>`, `<LSGT>`, and `<KPEN>`. It must not infer XKB
identity from `PhysicalKey.ScanCode`: that field carries a set-1 scan code and does not encode the
XKB key-name convention.

Explicit ISO-105 and ANSI-104 maps make international and keypad differences reviewable. Missing
pairs produce key-linked target diagnostics.

### 9.2 Symbols format

The final artifact is a classic XKB text format v1 `xkb_symbols` component stored as
`symbols/<layout-id>`. V1 is selected for interoperability with both X11 tooling and Wayland clients.
The generator uses canonical keysym names for known special keys and deterministic Unicode `U...`
notation for character outputs. `NoSymbol` fills missing intermediate levels.

A component file is preferable to a self-contained `xkb_keymap` because it composes with the host's
standard keycodes, types, compatibility data, and rules. The generator does not copy or modify the
system XKB database. Automatic installation and activation are outside the build boundary. See
[`LINUX-XKB.md`](LINUX-XKB.md).

### 9.3 Verification

Managed validation always checks identifiers, key coverage, keysyms, and output structure. When
available, `xkbcli compile-keymap` verifies the generated component in an isolated include root
combined with the system defaults. Versions 1.9 and newer add `--test`; older versions compile and
discard the emitted full keymap. The verifier is mandatory in Linux integration CI but optional for
local generation, so XKB text can be produced on any supported host.

### 9.4 Import-derived per-user variants

The per-user variant path composes the implemented import and generation capabilities but is not a
mode of the existing standalone build. It begins only with a system-origin import and keeps the
original layout and variant as an immutable derivation base:

```text
KeyboardProjectDocument + LayoutDerivation + XkbUserVariantMetadata
                              |
                              v
                    KeyboardLayoutDiffer
                              |
                              v
                    XkbUserVariantTranslator
                              |
                              v
                    XkbUserBundleGenerator
                              |
                              v
            staged user XKB root -> verify -> explicit install
```

`LayoutDerivation` lives in the document envelope, not in the Core aggregate. It records the system
base identity and an immutable snapshot of every representable mapping at import time. Core owns
only the platform-neutral difference operation. The Linux backend translates changed keys into a
section that includes the current system base with `%S`, then emits each changed key's complete
supported mapping.

The installed user root separates definition from language association:

```text
${XDG_CONFIG_HOME:-$HOME/.config}/xkb/
  symbols/keyboardstudio   app-owned generated definitions
  symbols/pl               shared, managed bridge sections
  symbols/al               shared, managed bridge sections
  rules/evdev.xml          minimal registry variants under system layouts
```

Normal builds write only to build output. `XkbUserInstallService` is a separate Linux boundary that
requires an explicit user action, verifies a staged merged root with `xkbcli`, creates backups and a
journal, performs conflict-aware writes, and rolls the transaction back on failure. Host-local
ownership and hashes live under `${XDG_STATE_HOME:-$HOME/.local/state}/keyboardstudio/xkb`, never in
the portable project document. Installation never writes a system XKB root and never activates a
desktop layout.

Managed installation is capability-gated: the first version targets Wayland/libxkbcommon user
configuration, requires `%S` support and external compilation verification, and treats X11 or an
old/unknown toolchain as export-only. Desktop discovery is a separate capability because only tools
using libxkbregistry merge user `evdev.xml` entries reliably. The complete contract is in
[`LINUX-USER-XKB-VARIANTS.md`](LINUX-USER-XKB-VARIANTS.md).

---

## 10. Build orchestration

### 10.1 Target dispatch

Target-specific generation, tooling, and verification live behind one backend interface rather than
in the orchestrator:

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
| `LinuxXkb` | symbols generation -> write | none | `xkbcli` when available; required in CI | `symbols/<layout-id>` |

This is single-target dispatch, not host dispatch. The XKB artifact is deterministic text, so
generation itself needs no host tooling; only the optional external verification stage does.

### 10.2 Target profiles

`BuildOptions.Target` chooses the output kind. The associated profile supplies backend metadata:

```text
LinuxXkb -> XkbLayoutMetadata
```

Profiles belong to the application/project-document boundary and remain separate from Core metadata.
Changing the target does not mutate key mappings.

### 10.3 Shared build collaborators

`KeyboardStudio.Build` keeps only what is target-neutral: the orchestrator, the backend resolver, the
stage/diagnostic vocabulary, and `IProcessRunner` — a cancellable, argument-list wrapper for invoking
external tools, used by the XKB verification and install-capability probes.

Anything that knows a target's file formats, tooling, or verification rules belongs in that target's
project, behind `IBuildBackend`.

### 10.4 Target-neutral results and stages

At the orchestration/UI boundary, result names describe an artifact rather than assuming compilation.
`KeyboardBuildResult.Artifact` contains:

```text
Success and artifact path
Common and target diagnostics
Raw/verifier log and retained log path
Manifest path
Artifact SHA-256
Typed backend details
```

XKB verifier messages map to the target-neutral diagnostic envelope rather than being presented as
compiler output.

Backends report named stage transitions through `IProgress<BuildStageProgress>`. The application
renders exactly those reports: Linux emits XKB generation, artifact writing, and verification. The
orchestrator owns common validation and the terminal completed, failed, or cancelled state.
Cancellation flows from the build panel to generators and external processes through the invocation
token.

`ArtifactBuildResult.GeneratedFiles` carries the deterministic generated XKB text. The UI can inspect
those snapshots, open the selected output
directory, and copy the combined structured diagnostics/raw log or canonical artifact path through
`IBuildInteractionService`; platform shell and clipboard APIs stay out of the ViewModel.

The build panel normalizes readiness issues, backend diagnostics, and generation exceptions into
seven user-facing problem kinds: project validation, target compatibility, source generation,
missing required toolchain, optional verifier unavailable, external tool failure, and artifact
verification. Codes and original messages remain visible. `KSL004` produces an unverified-success
presentation rather than failure when external verification is optional.

---

## 11. Keyboard templates

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

## 12. Layout import

*Adopted design, built by Phase 13, which is complete: the neutral contract, the seed, the whole XKB
pipeline, the fidelity report, provenance, the startup import, and the coverage that grades them —
goldens over a pinned copy of xkeyboard-config, an import-generate-import round trip, a soak over
everything the host advertises, and `xkbcli` as a conformance oracle.*

A new document must never open as bare geometry with zero mappings. Import supplies a starting point,
either from an embedded seed or from a layout already installed on the host. Full design detail lives
in [`LINUX-LAYOUT-IMPORT.md`](LINUX-LAYOUT-IMPORT.md).

### 12.1 Neutral contract, platform sources

Import yields a `KeyboardProject`, which is a Core concept, so the contract lives in Core and names a
layout only by opaque identifiers. Core acquires no XKB vocabulary.

```csharp
public interface ILayoutImportSource
{
    string Id { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }

    Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default);
}
```

`ILayoutImportCatalog` aggregates the registered sources and is the only import type the ViewModels
see, mirroring how `IBuildBackendResolver` keeps backends out of `BuildViewModel`. The Linux source is
registered at the composition root. A future source for another layout format implements the same
interface without reshaping the editor.

### 12.2 Seed project

A `us-basic` seed project is the content of every new document. It is host-independent, so the
empty-keyboard state cannot occur on any platform, including hosts with no XKB data at all.

`KeyboardStudio.Core` owns the contract — `ISeedProjectSource`, `SeedProjectId`,
`SeedProjectException` — and `KeyboardStudio.Persistence` owns `EmbeddedSeedProjectSource`, which
embeds `templates/seeds/us-basic.kbdproj` and reads it through the same DTOs and mapper that read a
user's `.kbdproj`. The implementation sits in the persistence assembly rather than beside its
contract because the seed is stored in the project file format, and 3's rule that Core must not know
the storage format outranks keeping the two files adjacent. The alternative — a second parser in
Core — would drift from the real one.

`ISeedProjectSource.Create` is synchronous and returns a fresh object graph per call. A new document
is created from the application's constructor, where nothing can be awaited, and the seed is an
embedded resource with no I/O latency; returning a shared instance would leak one document's edits
into the next, because `KeyboardLayout.Mappings` and `KeyMapping.Outputs` are mutable by design.

The seed is authored against `iso-105`. On any other geometry the mappings whose physical key is
absent are dropped, so `ansi-104` starts with 103 of its 104 keys mapped and its `Backslash` key
blank — ANSI names that key differently from the ISO key carrying the same characters.

The seed's geometry is generated from `templates/iso-105.json` by
`scripts/generate-us-basic-seed.py`, and a test asserts the two agree key-for-key. Without that
guard the repository would carry two copies of the same keyboard, free to disagree.

### 12.3 Linux XKB import pipeline

```text
ordered XKB data roots
        |
        v
rules/evdev.xml  -->  catalog of layout + variant descriptors
        |
   user selects one
        |
        v
symbols/<layout>  -->  lex  -->  parse  -->  section "<variant>"
        |
        v
include graph resolution (merge modes, cycle detection, depth cap)
        |
        v
ResolvedXkbSymbols: XKB key name -> ordered Group1 levels
        |
        +--> XkbKeyNameResolver   <AC01>  -> (template, "KeyA")
        +--> XkbKeysymDecoder     aogonek -> CharacterOutput("ą")
        +--> level index          1..4    -> ModifierLayer
        |
        v
KeyboardProject + LayoutImportReport
```

Resolution is a managed, deterministic transformation. `xkbcli` is not a runtime dependency; it is
absent from most desktop installs and [AD-017](DECISIONS.md) already fixes it as an optional
verifier. It is used instead as a CI conformance oracle against the managed resolver.

Data-root discovery and registry reading are built. `XkbDataRootLocator` takes `IXkbEnvironment`
and `IXkbFileSystem` as constructor arguments so libxkbcommon's search order is unit-testable
without an XKB installation and without mutating the test host's environment. `IXkbFileSystem`
exposes no way to create or modify anything, which keeps import's read-only boundary a property of
the interface rather than a rule implementers must remember. `XkbRulesRegistryReader` parses
`rules/evdev.xml` with `DtdProcessing.Ignore` and a null `XmlResolver`; the file declares
`SYSTEM "xkb.dtd"`, and a resolver would fetch an external entity from a path the application does
not own. A root with no registry is empty rather than failing — roots legitimately carry symbols
without rules — but a registry that will not parse throws, because listing nothing silently would
leave the user hunting for a layout they can see is installed.

`XkbSymbolsLexer` and `XkbSymbolsParser` read a symbols file into sections and statements. The
parser accepts the whole XKB statement vocabulary but consumes only what the domain model can hold,
and the split between the two ways it discards a statement is the design: a construct that cannot
change which character a key produces — a key type, a modifier map, a virtual modifier — is
recognized and dropped in silence, while one that can, such as an action or an overlay, is dropped
with a finding. What it does not recognize at all is skipped to the next `;` with `KSI022` rather
than aborting the file. Over the 199-file corpus of a current `xkeyboard-config` that last case does
not occur, and a corpus test holds it there: `KSI022` marks a real gap in the grammar, not
acceptable noise.

Parsing never throws. A truncated file, an unterminated string, a key missing its terminator — each
costs its own statement and nothing after it, because a layout that is 95% readable is a better
starting point than a refusal.

`XkbIncludeResolver` and `XkbSymbolsResolver` flatten the include graph. Real layouts are composed
rather than written out — `pl(basic)` is `latin` plus its own changes, and `latin` is itself built
from `kpdl` and `level3` — so flattening the chain is what turns a few dozen overrides into the
layout a user actually types on. Include strings are read as merge expressions rather than as file
names: `+` composes with `override` and `|` with `augment`, a merge keyword can stand in for
`include` entirely, and a `:2` suffix targets a second group the model does not hold, so it is
skipped with `KSI020` rather than flattened into group 1 and silently overwriting the layout the
user asked for.

Cycle detection keys on `(resolved path, section name)` rather than on the file. A file including
another of its own sections is ordinary — `pl(lefty)` includes `pl(basic)` — so a file-granular
visited set would break those layouts while still missing cycles that run through two files. Depth
is capped at 16 and a genuine cycle stops that branch, both reported and both leaving the rest of
the layout intact. Resolution retains the ordered include chain and records, per key, which
`file(section)` won, because composition otherwise makes a surprising output very hard to trace.

Over the same 199-file corpus every one of the 1,673 sections resolves and every include they name
is found: `KSI025` and `KSI024` are absent, and a corpus test holds them at zero, so either
appearing means the resolver lost its way rather than that the distribution shipped a broken layout.

`XkbKeysymDecoder` turns keysym names back into outputs, inverting `XkbKeysymMapper`. Its table is
generated at development time from pinned upstream sources under `third_party/keysyms` and committed
as `XkbKeysymTable.g.cs`, because no keysym header ships with a desktop system and an import that
needed one would fail on exactly the machines it exists to serve. CI regenerates the table and diffs
it, so a stale table cannot quietly import layouts that look plausible and are wrong.

The order the decoder tries things in carries the design. Function keys are read before the character
table, because `Return`, `Tab` and `KP_Multiply` all carry Unicode annotations upstream and would
otherwise import as control characters or a stray asterisk. Letters and digits are deliberately not
function keys, for the mirror reason: `a` must stay the character `a`, or a Dvorak import would label
every key by its physical position and lose the layout. Dead keys are dropped with `KSI031` and
keysyms outside the model with `KSI032`, and the result says which of the two happened even though
the code is shared, because "your model has no volume key" and "this file is broken" call for
different responses. Where the decoder could have invented a rule it follows libxkbcommon instead —
one to eight hex digits after `U`, the four case-insensitive empty-level keywords, the `XF86_`
underscore that XKeysymDB added and the headers never had — so that import describes what the user's
own machine does rather than what would have been tidier.

Over the same corpus the decoder reads 173,528 characters, 14,177 keys and 7,259 dead keys, and
recognises every keysym the corpus names. The 505 it cannot represent are media, IME and vendor keys,
each reported rather than dropped.

`XkbKeyNameResolver` resolves XKB key names through the same tables `XkbKeyNameMapper` already uses
for generation: `IXkbKeyNameMapper` exposes a table accessor and both directions derive from that one
source, so they cannot disagree about keys such as `<LSGT>`, which exists on `iso-105` and not on
`ansi-104`. XKB names stay out of Core and are still never inferred from `PhysicalKey.ScanCode`,
preserving [AD-018](DECISIONS.md).

Inverting the table is not sufficient on its own, because a key has as many names as the host's
keycodes file gives it and the table holds only the one generation writes. The aliases `keycodes/evdev`
declares are therefore folded in, in both directions and to a fixed point — `<I135>` reaches `<MENU>`
only by way of `<COMP>` — and the phonetic `<LatA>`–`<LatZ>` aliases with them. Those last are
ambiguous by construction: `keycodes/aliases` defines them three times over and `rules/evdev` picks
the set from the layout being loaded, so `<LatZ>` is the bottom-row key of a US keyboard and the
top-row key of a German one. Import makes the same choice from the layout name, because reading
`symbols/de`'s phonetic Russian variants with the wrong set returns them with Y and Z transposed.
A name that reaches no key of the chosen template is skipped with `KSI033` and counted in
`KeysSkipped`; that is informational rather than a warning, since a symbols file naming keys a
keyboard does not have is the ordinary case.

Over the same corpus the resolver lands 66,151 keys and skips 7,360, and every skipped name is a key
no PC keyboard has: media and vendor keys, `<FK13>` and above, and the extra keys of Japanese,
Brazilian and Sun keyboards. Corpus tests check the alias tables against the host's own `keycodes`
and `rules` files rather than against our reading of them.

Because two names can reach one key, the importer keys its mappings by physical key rather than by
the name a statement was written under, and a second statement for the same key replaces the first.
A phonetic layout is where this shows: `am(phonetic)` writes both `<LatQ>` and `<AD01>`, which
`keycodes/evdev` declares to be one key, and the host reads the later statement as the one that
takes effect. Adding both instead produces a document with two mappings for one key, which the
editor's own validation refuses.

### 12.4 Import is lossy and reports its losses

The domain model has no dead keys, no groups beyond the first, and no levels beyond four. Import does
not fail on them; it drops them and says so, because a starting point that is 95% correct is more
useful than a refused import.

```csharp
public sealed record LayoutImportReport(
    LayoutImportFidelity Fidelity,          // Exact | Reduced | Partial
    int KeysImported,
    int KeysSkipped,
    IReadOnlyList<string> ResolvedIncludeChain,
    IReadOnlyList<LayoutImportDiagnostic> Diagnostics);
```

`LayoutImportDiagnostic` reuses `ValidationSeverity` and carries the key ID and modifier layer, so
import findings render through the existing diagnostics list with a working jump-to-key target. The
resolved include chain is retained so an import can be explained and reproduced.

The fidelity level is not each source's own judgement. `LayoutImportReport.Classify` derives it:
any skipped key makes an import `Partial`, any finding above `Info` makes it `Reduced`, and anything
else is `Exact`. The `KSI` codes it grades are declared in Core, in `LayoutImportDiagnosticCodes`,
because import loss is a property of the domain model rather than of one platform's file format —
which is also why their wording names no format. This is the one place the import diagnostics differ
from the `KSL` build diagnostics, which belong to the backend that raises them.

### 12.5 Provenance and round-tripping

Import provenance is editor bookkeeping rather than layout semantics, so it lives in the document
envelope alongside target profiles, not in `ProjectMetadata`:

```text
KeyboardProjectDocument
 |- documentSchemaVersion        2; version 1 migrates forward
 |- project
 |- targets
 `- importProvenance
     |- sourceId / layoutId / variantId
     |- sourceLocation / sourceDescription
     `- importedAtUtc
```

Provenance records what the source said at the time and is never re-read on load. The layout it
names may since have been edited, upgraded, or uninstalled, and a record of where something came
from has to keep saying so even when the answer has changed. `ProjectDocumentService` owns it beside
the current path and dirty flag, so every save carries it without the editor having to remember to.

Import also pre-fills the `XkbLayoutMetadata` profile so an imported layout can be built straight
back out: the layout ID becomes `pl-custom`, the variant becomes the section, and the registry's
description becomes the description. The generated layout ID is always suffixed and never reuses the
source ID, because an artifact named `symbols/pl` would shadow the distribution's own file if copied
into an XKB root.

An import commits one of two ways. **A new project** replaces the document outright — new geometry,
new mappings, default build settings, no file path — and is what a fresh start means. **Replacement
mappings** keep the open document's keyboard, its build settings and the file it is saved as, and
change only what the keys produce; the dialog pins the geometry to that document's own, because a
second geometry there would only invite keys that cannot fit. Both paths run the existing
unsaved-changes prompt, after the dialog rather than before it: both discard work in progress, and
neither is worth prompting about until the user has said which one they want.

The dialog imports but never commits. Selecting a layout imports it immediately and shows the
result — a fidelity report the user cannot see until after they commit is a report they cannot act
on — and what to do with the document is decided by `MainWindowViewModel`, where the document lives.

### 12.6 Startup

The seed project loads first so the first frame never waits on the filesystem. On Linux the host's
configured layout is then resolved and imported asynchronously, replacing the seed only while the
document is still pristine. Detection reads `XKB_DEFAULT_LAYOUT`, then
`/etc/X11/xorg.conf.d/00-keyboard.conf`, then `/etc/vconsole.conf`, then `/etc/default/keyboard`,
then falls back to `us`. No process is spawned: `localectl` and `gsettings` would work but add a
process dependency to startup and write those same files anyway. Any failure is silent apart from a
diagnostics entry — a broken host XKB database must never stop the editor from opening.

Detection is `IXkbActiveLayoutProbe` in `KeyboardStudio.Linux`, which answers in XKB's own
vocabulary because that is what it reads. Core sees `IHostLayoutProbe`, which returns an
`ImportableLayoutReference` or nothing, and `XkbHostLayoutProbe` is the single line between them.
The split is what keeps the detection rules testable against files with no catalog present, and
leaves room for a second platform to answer the same question its own way.

`MainWindowViewModel.ImportHostLayoutAsync` is started by `App` after the window exists and is not
awaited. It replaces the document only when that document is still the untouched one the constructor
made — same instance, not dirty, no path — because a user who has already started working has said
what they want, and swapping it out a moment later would be worse than never importing. Its result
is a document in exactly the state an accepted dialog import leaves: no path, not dirty, XKB profile
pre-filled.

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

### KeyboardStudio.Build.Tests

Test the target-neutral pipeline against stub backends, so the contract stays provable without any
host toolchain:

- backend resolution, including duplicate and missing target registrations;
- that common validation errors stop a build before it reaches a backend, and that non-blocking
  severities do not;
- the terminal stage sequence, including the cancelled path;
- the shared external-process runner, including cancellation.

### KeyboardStudio.Linux.Tests

Test:

- template key ID to XKB key-name translation;
- logical/special key to keysym translation;
- two- and four-level modifier behavior;
- Unicode keysym generation;
- deterministic symbols-component golden files;
- `xkbcli` verification in Linux integration tests;
- symbols lexing, parsing, and include resolution including merge modes, self-referencing sections,
  genuine cycles, and the depth cap;
- registry reading with DTD resolution disabled;
- keysym decoding, asserted exhaustively symmetric with `XkbKeysymMapper`;
- golden imports of vendored `us`, `pl`, `de`, and `fr` fixtures, pinned so results do not depend on
  the host's installed xkeyboard-config version;
- a full import to generation to re-import round trip asserting model equality;
- a Linux CI soak test importing every layout and variant the host registry advertises;
- an `xkbcli` conformance oracle comparing resolved key/level tables, skipped when the tool is
  absent.

### KeyboardStudio.App.Tests

Test target selection, backend resolution, target-specific command enablement, dynamic stage
presentation, cancellation, and result/error presentation without referencing concrete generators.

Also test target presentation as behavior rather than markup: with one registered target the target
selector is absent and the XKB profile is the edited one.

Layout import is tested against a fake `ILayoutImportCatalog`: catalog listing and filtering, variant
selection, geometry override, fidelity presentation, import-as-new versus replace-mappings, the
unsaved-changes confirmation path, and the startup seed and host-import fallback chain.

---

## 14. Initial MVP boundary

### Included

- display ISO/ANSI physical keyboard;
- open every new document on a populated layout rather than bare geometry;
- list and import layouts installed on the host, with a previewed fidelity report;
- select keys;
- map physical key to logical key;
- map `Default`, `Shift`, `AltGr`, and `ShiftAltGr`;
- Unicode character outputs;
- save/load `.kbdproj` and target profiles;
- common and target-specific validation;
- deterministic Linux XKB v1 symbols generation;
- structural and optional external tool verification of the generated artifact.

### Excluded

- automatic XKB installation, desktop registration, or activation;
- dead keys;
- chained dead keys;
- ligatures;
- macros;
- arbitrary scripts;
- IMEs;
- runtime hooks/remapping;
- importing existing native artifacts;
- importing geometry: physical layout still comes from the bundled templates;
- editing, installing, or activating anything in a system or session XKB root.

The list above is the completed MVP boundary. Phase 14 adds only explicit, transactional
**per-user** XKB variant installation. System-wide installation, X11 installation, and activation
remain excluded.

Import is deliberately included in the boundary while dead keys are not, which is why import is
specified as lossy: it drops what the model cannot hold and reports each loss (12.4).

The domain model should remain extensible enough to add these later without complicating the first implementation.

---

## 15. Critical abstractions

```text
IKeyboardProjectStore
IKeyboardProjectValidator
IBuildBackendResolver
IBuildBackend
IProcessRunner                 (shared external-tool invocation)
ILayoutImportCatalog           (the only import type ViewModels see)
ILayoutImportSource            (one per platform layout source)
IApplicationSettingsStore      (host-local application preferences)
IApplicationThemeService       (neutral theme choice to Avalonia variant)
IStartupLayoutLoader           (host detection/import without document mutation)
```

These boundaries protect the editor from persistence and platform-specific implementation details.

---

## 16. Architectural summary

```text
KeyboardStudio.App (composition + target-aware UI)
 |
 +--> ILayoutImportCatalog
 |         `--> KeyboardStudio.Linux/Import
 |                  XKB roots -> registry -> symbols parse -> include resolve
 |                  -> keysym/key-name decode -> KeyboardProject + fidelity report
 |
 +--> KeyboardStudio.Persistence --> KeyboardStudio.Core
 |
 +--> KeyboardStudio.Build --> common validation --> backend resolver
 |                                                        |
 |                                                        v
 |                                              KeyboardStudio.Linux
 |                                              XKB symbols generation
 |                                              artifact writer
 |                                              xkbcli verification
 |                                                        |
 |                                                        v
 |                                              symbols/<layout-id>
 |
 `--> KeyboardStudio.Core
      KeyboardProject -> PhysicalKeyboard + KeyboardLayout + ProjectMetadata
```

---

## 17. Application themes, local settings, and startup shell

An application-owned appearance and startup layer sits above the dependency direction described
above without changing it. Theme choice is host-local presentation state, not keyboard domain state
and not a portable project setting.

```text
Local settings JSON
        |
        v
IApplicationSettingsStore
        |
        +----> IApplicationThemeService ----> Application.RequestedThemeVariant
        |                                              |
        |                                              v
        |                                  semantic DynamicResources
        |
        `----> AppearanceViewModel ----> selection and immediate persistence

IStartupLayoutLoader ----> structured startup result
                                  |
                                  v
MainWindowViewModel ----> adopt only while the startup document is untouched
```

White and Gray are custom Avalonia variants inheriting Light; Black inherits Dark. The inherited
variant supplies complete Fluent control behavior while KeyboardStudio defines one identical
semantic resource-key contract for all three palettes. Theme-dependent resources are always dynamic,
and views contain no literal presentation colors.

The settings store writes a versioned `settings.json` beneath the current user's local
application-data directory. Missing, invalid, inaccessible, unknown, or future settings fall back to
Gray without preventing startup. The composition root applies the loaded choice before constructing
the first window, avoiding a first-frame theme flash. Changing appearance never marks a `.kbdproj`
dirty or changes its serialized form.

Startup layout loading becomes a data-returning service. The populated seed remains the immediate,
editable first frame; on a supported Linux host, the loader imports the active layout asynchronously.
Only `MainWindowViewModel`, as document owner, may replace the seed, and only while it is still the
same clean, pathless startup document. This retains the current protection against a late background
result overwriting user work.

The standalone File menu row moves into the application header as an accessible vector icon beside
`KeyboardStudio`. Theme selection uses a separate accessible Appearance trigger. The permanent
`New from / Create` toolbar group is removed because a fresh window already contains the layout the
user should edit; explicit New and geometry choices remain secondary File-menu operations.

`ApplicationThemeTokens` is the required key set, and tests hold all three dictionaries to it, audit
every view for a colour of its own, and reject any resource key KeyboardStudio does not define
itself. One exception exists: `FluentTheme.Palettes` pins the accent Fluent uses for its own check
marks, list selection and scrollbars, which otherwise follows the desktop's accent colour (AD-038).

Diagnostics collapse to one line while there is nothing to report and expand on the transition into
error, so the keyboard keeps the room a permanently reserved panel used to take (AD-039). Emphasis
is a contract rather than a decoration: Build and Install are the only primary actions, Uninstall
and Discard the only destructive ones, and markup tests hold those lists along with the focus order,
the accessible name on every icon-only control, and the floor on supporting-text size.

The detailed resource contract, failure behavior, UX scope, and acceptance criteria are in
[`THEMING.md`](THEMING.md). The ordered work and test gates are in
[`THEMING-IMPLEMENTATION-PLAN.md`](THEMING-IMPLEMENTATION-PLAN.md).
