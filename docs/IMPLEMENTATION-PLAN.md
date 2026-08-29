# KeyboardStudio Implementation Plan

## 1. Purpose

This document is the executable implementation plan for KeyboardStudio. The completed phases provide
the solution, Avalonia editor, versioned project persistence, validation, Windows semantic translation,
deterministic native source generation, the MSVC/WDK compile/link pipeline, structural artifact
verification, a verified Linux XKB output backend, and the target-aware build user experience. The
remaining work is Windows integration CI and release stabilization.

The goal is to move from that bootstrap state to a usable first release that can:

1. display realistic ISO/ANSI physical keyboard templates;
2. edit key mappings for `Default`, `Shift`, `AltGr`, and `Shift+AltGr`;
3. create, save, load, and validate `.kbdproj` projects;
4. translate the platform-neutral project into either Windows keyboard tables or Linux XKB symbols;
5. compile Windows tables into a native keyboard-layout DLL or materialize a portable XKB layout file;
6. expose target selection and the build process from the Avalonia application with useful diagnostics;
7. verify both artifact paths through automated tests and platform integration CI.

The plan intentionally keeps installation/registry registration, dead keys, ligatures, macros, IMEs, and runtime remapping outside the first release.

---

## 2. Current baseline

The current Phase 10-complete baseline provides:

- `KeyboardStudio.slnx`, using the modern XML solution format and targeting .NET 10;
- `KeyboardStudio.App` using Avalonia;
- `KeyboardStudio.Core` with keyboard/project domain objects, editing, templates, and validation;
- `KeyboardStudio.Persistence` with versioned DTO-based JSON persistence and migrations;
- `KeyboardStudio.Build` with orchestration, isolated workspaces, process execution, MSVC/SDK
  discovery, PE/export/load verification, manifests, and opt-in reproducibility checks;
- `KeyboardStudio.Windows` with semantic translation and deterministic `KBDTABLES` source generation;
- `KeyboardStudio.Linux` with typed XKB translation, deterministic v1 symbols generation, manifests,
  managed verification, and optional `xkbcli` compilation;
- complete ISO-105 and ANSI-104 templates;
- Core, Windows, Linux, and App test projects;
- Linux-hosted restore/build/test validation;
- an Avalonia editor with project lifecycle, mapping controls, and diagnostics;
- a target-aware build panel with per-target profiles, preflight gating, backend-reported stages,
  cancellation, generated-source/output actions, and categorized result presentation.

Phases 0-10 are complete. The Windows path generates real `KBDTABLES` source, compiles x64
DLLs through a discovered MSVC/Windows SDK toolchain, and verifies the resulting artifact beyond the
linker exit code. `BuildOrchestrator` now validates once and resolves one `IBuildBackend` for the
selected target. The Linux path materializes an XKB symbols component directly, performs managed
validation everywhere, and compiles it with `xkbcli` when available without probing MSVC.

---

## 3. Delivery principles

### 3.1 Keep Core platform-neutral

`KeyboardStudio.Core` must never acquire references to:

- Avalonia;
- Win32 APIs;
- WDK headers;
- `kbd.h` structures;
- MSVC command-line details;
- XKB key names, keysyms, libxkbcommon APIs, or XKB installation paths;
- registry installation code.

Windows-specific knowledge belongs in `KeyboardStudio.Windows`, XKB-specific knowledge belongs in
`KeyboardStudio.Linux`, and shared orchestration/tool execution belongs in `KeyboardStudio.Build`.

### 3.2 Prefer deterministic transformations

Every important transformation should be a pure or near-pure function:

```text
KeyboardProject
    -> validation result
    -> selected target backend
    -> target intermediate model
    -> generated artifact files
    -> optional compiler or verifier invocation
    -> build result
```

The same project and options must produce byte-for-byte identical generated source.

### 3.3 Add behavior behind tests first

Each phase below has a test gate. A phase is not considered complete merely because the UI appears to work.

### 3.4 Avoid premature advanced layout features

Version 1 supports only direct character outputs and special logical keys across four modifier layers. Dead keys and composition features must not distort the first implementation.

### 3.5 Preserve readable generated artifacts

Generated Windows C and Linux XKB text are debugging and review surfaces. They should be
human-readable, stable, and easy to compare against the authoritative platform formats.

---

## 4. High-level implementation sequence

```text
Phase 0  Baseline hardening
   |
   v
Phase 1  Project model + persistence completion
   |
   v
Phase 2  Physical keyboard templates + geometry
   |
   v
Phase 3  Editor interaction and project lifecycle
   |
   v
Phase 4  Validation and diagnostics
   |
   v
Phase 5  Windows semantic translation
   |
   v
Phase 6  Real KBDTABLES source generation
   |
   v
Phase 7  MSVC/WDK compiler integration
   |
   v
Phase 8  Artifact verification
   |
   v
Phase 9  Linux XKB layout file generation
   |
   v
Phase 10 Target-aware build UX
   |
   v
Phase 11 Windows integration CI
   |
   v
Phase 12 MVP stabilization and release readiness
   |
   v
Phase 13 Linux focus and layout import
```

Phase 13 narrows the shipping user interface to the Linux target and removes the empty-keyboard
starting state. It deliberately hides rather than deletes the Windows path, so Phases 5-8 and 11 stay
green and the target can be re-exposed by a policy change.

The Linux phase follows Windows artifact verification so the completed Windows path remains intact,
but precedes build UX so target selection is designed once for both outputs. Linux integration coverage
is part of Phase 9 because it runs on the existing Linux CI host; Windows native CI remains separate.
Neither backend may force its key names, metadata, modifier bits, or toolchain concepts into the core
project model.

---

# Phase 0 — Baseline hardening

## Objective

Turn the current skeleton into a stable development baseline before deeper functionality is added.

## Work items

### P0.1 Normalize namespaces and file organization

Split large bootstrap files into focused units where useful:

```text
KeyboardStudio.Core/
  Projects/
  Keyboards/
  Mappings/
  Editing/
  Validation/

KeyboardStudio.App/
  Views/
  ViewModels/
  Controls/
  Services/

KeyboardStudio.Windows/
  Translation/
  Model/
  Generation/

KeyboardStudio.Build/
  Abstractions/
  Orchestration/
  Toolchains/
```

Do not over-fragment tiny types, but avoid maintaining major subsystems in single monolithic files.

### P0.2 Strengthen compiler settings

Add/review:

- warnings as errors for project code;
- deterministic builds;
- nullable reference types enabled;
- analyzers supported by the SDK;
- consistent formatting via `.editorconfig`;
- explicit release/debug configurations where required.

### P0.3 Improve CI matrix structure

Keep the existing Ubuntu build because it proves the core and Avalonia projects remain cross-platform.

Prepare CI so a Windows job can later be added without duplicating common steps.

### P0.4 Establish test naming conventions

Use behavior-oriented names such as:

```text
MapCharacter_WhenMappingExists_ReplacesOutput
LoadAsync_WhenSchemaVersionUnknown_RejectsProject
Translate_WhenAltGrCharacterExists_MapsExpectedModifierState
Generate_WhenInputIsIdentical_ProducesIdenticalSource
```

## Acceptance criteria

- solution builds with zero warnings in Release;
- all current tests pass;
- CI remains green on Ubuntu;
- folder organization reflects architecture boundaries;
- no functional behavior is lost.

---

# Phase 1 — Complete project model and persistence

## Objective

Make `.kbdproj` a durable project format that can safely evolve.

## Work items

### P1.1 Finalize project metadata

`ProjectMetadata` should contain at least:

- display name;
- description;
- project version;
- language/locale metadata;
- Windows layout identifier/name where applicable;
- author metadata only if useful for generated resources.

Separate general project metadata from Windows-only build metadata if a field has no cross-platform meaning.

### P1.2 Formalize schema versioning

Persist an explicit integer schema version.

Create:

```csharp
public static class KeyboardProjectSchema
{
    public const int CurrentVersion = 1;
}
```

Loading rules:

- current version -> load;
- known older version -> migrate;
- unknown future version -> reject with clear error;
- malformed project -> reject with structured diagnostic.

### P1.3 Introduce persistence DTOs

Do not depend forever on serializing the mutable domain classes directly.

Create persistence DTOs such as:

```text
KeyboardProjectDto
ProjectMetadataDto
KeyboardLayoutDto
KeyMappingDto
KeyOutputDto
```

Add explicit domain <-> DTO mapping.

This prevents future domain refactoring from silently breaking stored project files.

### P1.4 Define polymorphic output encoding

Represent output kinds explicitly, for example:

```json
{
  "kind": "character",
  "value": "ą"
}
```

and later:

```json
{
  "kind": "specialKey",
  "key": "Enter"
}
```

Do not use fragile runtime type names in serialized JSON.

### P1.5 File service abstraction for the application

Introduce an application-level project document service responsible for:

- New;
- Open;
- Save;
- Save As;
- current file path;
- dirty state;
- error presentation boundary.

The service may use Avalonia's storage provider in the App layer, but persistence serialization remains independent.

### P1.6 Project migrations

Create a migration abstraction now, even if v1 has no real migrations:

```csharp
IProjectMigration
ProjectMigrationPipeline
```

This avoids a future switch statement growing inside `JsonKeyboardProjectStore`.

## Tests

- round-trip all output kinds;
- Unicode characters survive save/load;
- schema version persists;
- unknown future schema is rejected;
- invalid JSON returns a meaningful failure;
- DTO mapping produces equivalent domain state;
- save/load is deterministic enough for stable diffs where possible.

## Acceptance criteria

A `.kbdproj` file can be created, saved, reopened, and compared to the original project without losing semantic information.

---

# Phase 2 — Physical keyboard templates and geometry

## Objective

Replace the demo/wrap-panel keyboard with reusable physical keyboard definitions.

## Work items

### P2.1 Define template schema

Template JSON should represent physical identity and geometry, not project-specific output.

Example shape:

```json
{
  "schemaVersion": 1,
  "id": "iso-105",
  "name": "ISO 105-key",
  "unitWidth": 54,
  "unitGap": 4,
  "keys": [
    {
      "id": "KeyA",
      "scanCode": 30,
      "x": 1.75,
      "y": 3,
      "width": 1,
      "height": 1
    }
  ]
}
```

The persisted template should use logical keyboard units, not pixels.

### P2.2 Implement `IKeyboardTemplateProvider`

Responsibilities:

- enumerate built-in templates;
- load template by ID;
- validate template schema;
- convert DTO to `PhysicalKeyboard`;
- cache immutable built-in templates.

### P2.3 Build ISO-105 template

Populate a complete ISO-105 physical layout with:

- scan codes;
- extended-key information;
- realistic width/height values;
- row geometry;
- stable IDs.

### P2.4 Build ANSI-104 template

Populate the complete ANSI template and confirm keys that differ physically from ISO are represented independently.

### P2.5 Render geometry in Avalonia

Replace `WrapPanel` rendering with a positioned control/panel.

Preferred approach:

```text
KeyboardView
  -> ItemsControl
  -> Canvas or custom KeyboardPanel
  -> KeyControl per key
```

Scale keyboard units to available space while maintaining aspect ratio.

### P2.6 Create reusable `KeyControl`

Responsibilities:

- display current output label;
- optionally display physical/logical key hint;
- selected state;
- unmapped state;
- validation/error state;
- click command.

It must not perform mapping mutations itself.

## Tests

- template parsing;
- duplicate physical IDs detected;
- duplicate scan code definitions rejected where invalid;
- geometry loads consistently;
- all expected ISO/ANSI key IDs exist;
- no project output is stored in template JSON.

## Acceptance criteria

The application displays a recognizable, correctly proportioned ISO-105 and ANSI-104 keyboard generated entirely from template data.

---

# Phase 3 — Editor interaction and project lifecycle

## Objective

Make the application usable for actual keyboard mapping work.

## Work items

### P3.1 Key selection state

Create clear selection behavior:

- one selected key at a time;
- selected key survives modifier-layer changes;
- selecting another key updates the mapping panel;
- selection is visual.

### P3.2 Modifier layer selection

Support exactly:

- Default;
- Shift;
- AltGr;
- Shift+AltGr.

Expose friendly labels while keeping enum values stable in Core.

### P3.3 Mapping panel

For the selected physical key display:

- physical key ID;
- scan code;
- logical key;
- current output for each layer;
- direct editing controls.

Prefer showing all four outputs at once in the details panel even if the keyboard visualization shows only the active layer.

### P3.4 Logical key editing

Provide a controlled way to map a physical key to a logical key/virtual-key concept.

The core logical-key model must be explicit enough to represent:

- letters;
- digits;
- punctuation;
- common non-character keys needed by a normal keyboard.

Do not model arbitrary Win32 virtual-key numbers in Core.

### P3.5 Character input validation

For v1, `CharacterOutput` should represent exactly one Unicode scalar/grapheme policy decided by the project.

Recommended MVP rule:

- accept one Unicode scalar value;
- reject empty string as character output (use `NoOutput` instead);
- reject multi-codepoint sequences until ligature/macro support is introduced.

Document the rule explicitly.

### P3.6 Clear/unmap operations

Users need separate operations for:

- clear output for one layer;
- clear all outputs for selected key;
- reset selected key to template/default mapping if default mappings are later introduced.

### P3.7 Dirty tracking

`KeyboardEditor` mutations should raise or return enough information for the document layer to set `IsDirty`.

Avoid dirty tracking through deep object graph property-change observation.

### P3.8 New/Open/Save/Save As

Add File commands and keyboard shortcuts:

- `Ctrl+N` New;
- `Ctrl+O` Open;
- `Ctrl+S` Save;
- `Ctrl+Shift+S` Save As.

Prompt before destructive document replacement when unsaved changes exist.

## Tests

Core tests:

- mapping all four layers;
- clearing one layer;
- changing logical key;
- invalid key ID behavior;
- invalid character output behavior.

ViewModel tests:

- selection updates details;
- active layer changes keyboard labels;
- mutation marks document dirty;
- save clears dirty flag.

## Acceptance criteria

A user can create a project, edit mappings visually, save it, reopen it, and continue editing without losing state.

---

# Phase 4 — Validation and diagnostics

## Objective

Ensure invalid projects are caught before Windows source generation.

## Work items

### P4.1 Validation pipeline

Refactor validation into composable rules:

```text
IKeyboardProjectValidationRule
  -> MetadataValidationRule
  -> PhysicalKeyboardValidationRule
  -> MappingValidationRule
  -> WindowsCompatibilityValidationRule (Windows assembly)
```

Core rules must remain platform-neutral.

### P4.2 Stable diagnostic codes

Use documented codes such as:

```text
KSP001  duplicate physical key ID
KSP002  invalid scan code
KSM001  mapping refers to missing key
KSM002  invalid character output
KSW001  unsupported Windows logical-key mapping
KSW002  unsupported Windows modifier combination
```

Codes should be stable enough for tests and future CLI integration.

### P4.3 Severity model

Support:

- Info;
- Warning;
- Error.

Only errors block build.

### P4.4 UI diagnostics

Add a diagnostics panel with:

- severity;
- code;
- message;
- key association where available.

Clicking a key-related diagnostic should select/highlight that key.

### P4.5 Continuous lightweight validation

Run cheap project validation after meaningful edits with debouncing if needed.

Do not invoke Windows source generation or native compilation on every edit.

## Tests

- every rule has direct tests;
- diagnostic codes are stable;
- errors block build orchestration;
- warnings do not block build;
- key-linked diagnostics preserve `KeyId`.

## Acceptance criteria

The user can see why a project cannot be built and navigate from an error to the affected key.

---

# Phase 5 — Windows semantic translation

## Objective

Translate the platform-neutral model into a complete Windows-specific intermediate representation before generating C.

This phase is the start of the critical Windows compiler path.

## Work items

### P5.1 Define Windows virtual-key model

Create an internal or public-to-Windows-assembly enum/value type representing the subset of Windows virtual keys required by v1.

Mapping must be explicit:

```text
LogicalKey.A -> VK_A
LogicalKey.Enter -> VK_RETURN
LogicalKey.Space -> VK_SPACE
...
```

No `Enum.Parse`/name coincidence shortcuts.

### P5.2 Define scan-code mapping model

Represent:

```csharp
VscToVkMapping
ExtendedVscToVkMapping
```

Account for normal and extended scan-code tables separately where required by Windows.

### P5.3 Define modifier model

Represent Windows modifier bits and modifier-number states required for:

- no modifiers;
- Shift;
- Ctrl;
- Alt;
- AltGr semantics;
- Shift+AltGr.

Although Core exposes only AltGr, Windows translation may need to model the Ctrl+Alt relationship used by Windows keyboard layouts.

### P5.4 Define character table rows

Create typed intermediate rows such as:

```text
WindowsCharacterMapping
  VirtualKey
  Attributes
  Default
  Shift
  AltGr
  ShiftAltGr
```

Choose the correct generated `VK_TO_WCHARS<n>` table width based on supported modifier states.

### P5.5 Special/non-character keys

Decide which logical keys require only scan-code -> virtual-key mapping and should not be included as printable character rows.

### P5.6 Unsupported mapping detection

Windows translation should fail with structured diagnostics rather than silently drop mappings.

## Tests

Create table-driven tests covering representative categories:

- letters;
- digits;
- punctuation;
- space;
- Enter/Tab/Backspace;
- AltGr Unicode output;
- Shift+AltGr output;
- extended keys;
- unmapped output.

## Acceptance criteria

A valid `KeyboardProject` translates into a complete, deterministic Windows intermediate model with no C code involved.

---

# Phase 6 — Real Windows `KBDTABLES` source generation

## Objective

Generate native C source equivalent in structure to a real WDK keyboard-layout implementation.

## Work items

### P6.1 Establish reference fixture

Select one or more Microsoft sample keyboard layouts as structural references.

Create notes mapping KeyboardStudio model concepts to Windows structures:

```text
Physical scan code       -> VSC_VK tables
Modifier mapping         -> VK_TO_BIT + MODIFIERS
Printable outputs        -> VK_TO_WCHARS<n>
Character table groups   -> VK_TO_WCHAR_TABLE
Layout descriptor        -> KBDTABLES
Entry point               -> KbdLayerDescriptor
```

Do not copy unnecessary sample-specific data.

### P6.2 Generate source file set

Target a deterministic set such as:

```text
<layout>.c
<layout>.h
<layout>.def
<layout>.rc
```

If fewer files are sufficient, document why.

### P6.3 Generate scan-code tables

Emit:

- primary scan-code table;
- E0 extended mappings if needed;
- E1 mappings if needed;
- sentinel rows expected by Windows structures.

### P6.4 Generate key names

Generate key name tables only where necessary for the layout and debugging/Windows behavior.

Keep display names separate from logical mapping semantics.

### P6.5 Generate modifier tables

Emit:

- `VK_TO_BIT` definitions;
- modifier-number mapping;
- invalid modifier states where appropriate;
- AltGr layout flags.

### P6.6 Generate character tables

Emit correct `VK_TO_WCHARS<n>` rows.

Requirements:

- deterministic ordering;
- escaped C literals or numeric Unicode values;
- correct handling of no-output sentinel values;
- explicit Unicode encoding policy;
- no locale-dependent formatting.

### P6.7 Generate `KBDTABLES`

Populate all required fields for the supported MVP feature subset.

Unused optional structures should be represented exactly as Windows expects, not guessed.

### P6.8 Generate `KbdLayerDescriptor`

Export the layout descriptor with the correct calling/export conventions required by the build.

### P6.9 Generate `.def` and resource metadata

Include exported function declaration and useful file/version metadata.

### P6.10 Golden-file tests

Maintain small readable golden fixtures under tests:

```text
tests/KeyboardStudio.Windows.Tests/Fixtures/
  MinimalUs/
  AltGrUnicode/
  IsoExample/
```

Compare normalized generated output exactly.

## Tests

- deterministic generation;
- C tables contain expected scan-code mappings;
- modifier table matches expected state numbers;
- Unicode code points are correct;
- exported descriptor exists;
- no generated source contains unstable timestamps/paths unless explicitly requested;
- golden fixtures match expected source.

## Acceptance criteria

Generated C source is structurally valid for the Windows keyboard-layout ABI and ready to compile using the WDK/MSVC toolchain.

---

# Phase 7 — MSVC/WDK compiler integration

## Objective

Turn generated source into a native keyboard-layout DLL.

## Work items

### P7.1 Implement build-environment detection

Create a Windows implementation of `IBuildEnvironment` that detects:

- Windows host;
- Visual Studio Build Tools / MSVC;
- Windows SDK/WDK headers and libraries;
- required tools such as `cl.exe`, `link.exe`, and resource compiler if used;
- supported target architectures.

Return a structured status rather than a boolean-only failure.

### P7.2 Resolve compiler environment

Prefer supported discovery mechanisms rather than hard-coded paths.

The resolved environment should expose:

```text
CompilerPath
LinkerPath
ResourceCompilerPath
IncludePaths
LibraryPaths
ToolVersion
SdkVersion
```

### P7.3 Build working directory

Each build gets an isolated directory:

```text
<project-build-root>/
  generated/
  obj/
  output/
  logs/
```

Never compile in the source repository directory.

### P7.4 Implement process runner

Create a reusable process execution abstraction capturing:

- executable;
- arguments;
- environment;
- working directory;
- stdout;
- stderr;
- exit code;
- duration;
- cancellation.

Avoid shell string concatenation where argument-list APIs are available.

### P7.5 Compile generated C

Compile with the expected Windows headers and target architecture.

Support x64. Other Windows architectures are outside the MVP scope.

### P7.6 Link keyboard-layout DLL

Produce a DLL exporting `KbdLayerDescriptor`.

Use deterministic naming derived from validated project build metadata.

### P7.7 Build logs

Map tool output into `CompilerMessage` objects while preserving the raw log for troubleshooting.

### P7.8 Cancellation and cleanup

A cancelled build should terminate child processes and leave either a useful diagnostic folder or clean temporary files according to a documented policy.

## Tests

Unit tests:

- environment detection parsing;
- command construction;
- process result mapping;
- missing toolchain diagnostics.

Windows integration tests:

- compile a minimal generated layout;
- output DLL exists;
- compiler exits successfully;
- known invalid source produces useful failure.

## Acceptance criteria

On a correctly configured Windows machine, `BuildOrchestrator` produces a native keyboard-layout DLL from a valid KeyboardStudio project.

---

# Phase 8 — Artifact verification

## Objective

Do not treat linker success as proof that the output is a valid keyboard-layout artifact.

## Work items

### P8.1 PE verification

After linking, verify:

- file exists;
- PE architecture matches requested target;
- DLL characteristic is present;
- expected export exists.

### P8.2 Export verification

Confirm `KbdLayerDescriptor` is exported under the expected name.

### P8.3 Load-level smoke test

On Windows CI, create a safe test helper that can load/inspect the DLL sufficiently to confirm the exported descriptor can be resolved.

Do not install/register the keyboard layout as part of normal unit tests.

### P8.4 Generated/source manifest

Return a build manifest containing:

```text
Project name
Build target
Generated source files
Compiler/toolchain versions
Output DLL path
Output hash
Build timestamp (manifest only, not generated source)
```

### P8.5 Reproducibility check

Where toolchain behavior permits, build the same project twice and compare generated source exactly and binary outputs as far as deterministic linker settings allow.

## Acceptance criteria

A successful build result means more than "link.exe returned 0"; the artifact passes structural verification.

---

# Phase 9 — Support for Linux XKB Layout File Generation

## Objective

Turn the same platform-neutral `KeyboardProject` used by the Windows backend into a deterministic,
installable XKB symbols component. This phase also generalizes build dispatch so one build invocation
selects either a Windows DLL backend or the Linux XKB backend without pretending that both targets
have a native compiler.

Place this phase after Windows artifact verification and before build UX. The completed Windows path
is therefore preserved, while target selection and target-specific settings exist before the GUI build
workflow is finalized.

## Work items

### P9.1 Generalize orchestration for heterogeneous artifact targets

Add `LinuxXkb` to `BuildTarget` and replace the fixed generator/environment/compiler tuple in
`BuildOrchestrator` with a target backend resolved from the selected target. The planned boundary is:

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

`BuildOrchestrator` continues to run platform-neutral validation once, resolves exactly one backend,
and delegates the target-specific validation, generation, materialization, and verification stages.
MSVC compilation remains an internal collaborator of the Windows backend. Do not introduce a no-op
`INativeCompiler` for XKB merely to satisfy the current pipeline shape.

### P9.2 Add the Linux/XKB backend and target metadata

Create:

```text
src/KeyboardStudio.Linux/
  Translation/
  Model/
  Generation/
  Verification/

tests/KeyboardStudio.Linux.Tests/
```

`KeyboardStudio.Linux` references `KeyboardStudio.Core` and `KeyboardStudio.Build`; neither Core nor
Persistence references the Linux backend. Define `XkbLayoutMetadata` for a sanitized layout ID,
section/variant ID, and display description. Windows and XKB metadata are separate target profiles
associated with a project document, not fields added to `ProjectMetadata`.

Extend the target-settings persistence boundary called for by AD-009 so a saved document can preserve
both Windows and XKB profiles using stable target discriminators. The domain aggregate remains
platform-neutral and usable without either profile.

### P9.3 Map physical key identities to XKB key names

Translate stable physical key IDs from the ISO-105 and ANSI-104 templates to XKB symbolic key names,
for example:

```text
KeyA             -> <AC01>
Digit1           -> <AE01>
IntlBackslash    -> <LSGT>
Enter            -> <RTRN>
NumpadEnter      -> <KPEN>
```

This mapping is a Linux-backend/template capability. Do not derive XKB names from the current set-1
scan-code value and do not add XKB identifiers to `KeyboardStudio.Core`. Unknown template/key pairs
fail with stable, key-linked `KSL` diagnostics rather than being silently omitted.

### P9.4 Translate project mappings to XKB keysyms and levels

Create a typed XKB intermediate model before writing text. Map the four core layers as follows:

| Core layer | XKB level | Modifier meaning |
|---|---:|---|
| `Default` | 1 | none |
| `Shift` | 2 | Shift |
| `AltGr` | 3 | LevelThree |
| `ShiftAltGr` | 4 | Shift+LevelThree |

Translate `LogicalKey` values to canonical non-character keysyms and character outputs to stable
keysym spelling. Prefer canonical keysym names where deliberately mapped; otherwise use deterministic
Unicode `Uxxxx`/`Uxxxxxxxx` notation. Use `NoSymbol` for absent intermediate levels and choose an
appropriate XKB key type (`ONE_LEVEL`, `TWO_LEVEL`, `FOUR_LEVEL`, or alphabetic equivalent). Include
the standard Right-Alt LevelThree switch only when levels 3/4 are used.

### P9.5 Generate a deterministic XKB symbols component

Emit classic XKB text format v1 for maximum interoperability with X11 tools and Wayland clients. The
primary artifact is a component file suitable for:

```text
<xkb-root>/symbols/<layout-id>
```

It contains a named, default `xkb_symbols` section, group display name, sorted key declarations,
required type annotations, and required standard includes. It does not embed host paths, timestamps,
session-specific keycodes, rules files, or system installation commands.

Generate a symbols component rather than a self-contained `xkb_keymap`: the component composes with
the host's normal `keycodes`, `types`, `compat`, and `rules` data and is the conventional unit for a
custom XKB layout. See [`LINUX-XKB.md`](LINUX-XKB.md) for the format and scope decisions.

### P9.6 Materialize the Linux artifact and build manifest

The XKB text file is the final Linux artifact. The Linux backend writes it directly to a controlled
output directory and returns an artifact result; it has no compile/link stage. Produce a manifest with:

```text
Project name
Build target (LinuxXkb)
Layout and section IDs
Generated symbols path
Output hash
Generator version
Verifier/tool version when available
Build timestamp (manifest only)
```

Keep target-neutral result language (`ArtifactPath`, diagnostics, stages) even if Windows
implementations continue to expose compiler-specific details below their backend boundary.

### P9.7 Validate generated layouts with `xkbcli`

Always run managed structural validation for identifiers, key names, keysyms, levels, and deterministic
output. When `xkbcli` is available, verify the component in an isolated XKB root using the generated
layout plus default system includes:

```text
xkbcli compile-keymap
  --include <workspace>/xkb
  --include-defaults
  --test
  --layout <layout-id>
```

Use `--test` on libxkbcommon 1.9 and newer. Older supported `xkbcli` versions compile normally and
the captured full-keymap stdout is discarded after verification.

Capture version, arguments, stdout/stderr, exit code, duration, and structured diagnostics. Missing
`xkbcli` does not prevent deterministic text generation on another host, but the result is marked as
not externally verified. Linux CI must install the tool and require verification. Never install or
activate the generated layout during build or test.

### P9.8 Add XKB unit and golden-file tests

Cover:

- ISO-105 and ANSI-104 physical-ID mappings;
- letters, punctuation, special keys, and keypad keys;
- two-level and four-level mappings;
- AltGr/Shift+AltGr behavior;
- BMP and supplementary-plane Unicode keysyms;
- unsupported key/template diagnostics;
- deterministic source and sanitized output names;
- target resolution proving Windows and Linux builds select different backends.

Keep representative golden files small and readable.

### P9.9 Add Linux XKB integration coverage

Add an `XkbIntegration` test category to the existing Linux CI job. Compile representative generated
symbols components with `xkbcli`, retain the generated file and verifier log on failure, and cover at
least an ISO layout with AltGr Unicode output plus an ANSI two-level layout.

## Acceptance criteria

- one `KeyboardProject` can be built as `WindowsX64` or `LinuxXkb` by changing only
  the selected target/profile;
- selecting `LinuxXkb` never probes MSVC or invokes `INativeCompiler`;
- the generated XKB v1 symbols component is deterministic and contains all supported mappings;
- representative artifacts compile successfully with `xkbcli` on Linux CI;
- Windows build behavior and tests remain unchanged;
- installation or activation of the XKB layout remains explicitly out of scope.

---

# Phase 10 — Target-aware build user experience

## Objective

Expose both artifact backends cleanly in Avalonia.

## Work items

### P10.1 Build panel

Display:

- selected target (`Windows x64` or `Linux XKB`);
- settings for the selected target profile;
- required tool/verifier availability;
- validation status;
- output directory;
- Build button;
- target-specific stage progress;
- final artifact path.

### P10.2 Disable invalid actions

Build is disabled when:

- the project has blocking common or selected-target validation errors;
- the selected backend cannot produce its artifact;
- required Windows tools are unavailable for a Windows target;
- a build is already running.

An unavailable optional XKB verifier is shown as a warning and does not make text generation depend on
the host OS.

### P10.3 Build diagnostics

Show only stages reported by the selected backend:

```text
Common:   Validating -> Generating -> Verifying -> Completed / Failed
Windows:  Validating -> Generating -> Compiling -> Linking -> Verifying -> Completed / Failed
Linux:    Validating -> Generating XKB -> Writing artifact -> Verifying (when available) -> Completed / Failed
```

### P10.4 Open generated files/output

Provide actions to:

- open output directory;
- inspect generated C or XKB text;
- copy diagnostic/build log;
- copy the canonical artifact path.

### P10.5 Error presentation

Distinguish:

- project validation error;
- target compatibility error;
- source-generation error;
- missing required toolchain;
- optional verifier unavailable;
- compiler/linker error;
- artifact verification error.

## Tests

- target and profile selection;
- target-specific Build command enablement;
- backend-specific state transitions;
- cancellation;
- success/failure/unverified result presentation;
- common validation errors prevent every backend invocation;
- target validation errors prevent only the selected backend.

## Acceptance criteria

A user can select Windows DLL or Linux XKB output, build a valid project, and understand the resulting
artifact and any failure without reading application source code.

---

# Phase 11 — Windows integration CI

## Objective

Continuously prove that generated source actually compiles on Windows.

## Work items

### P11.1 Add Windows runner

Extend GitHub Actions with a Windows job that:

1. restores;
2. builds .NET solution;
3. runs all unit tests;
4. locates the Windows build toolchain;
5. builds one or more fixture keyboard DLLs;
6. verifies exports/artifacts.

### P11.2 Separate fast and native tests

Use categories/traits:

```text
Unit
Golden
XkbIntegration
WindowsIntegration
```

Linux runs all platform-neutral tests plus `XkbIntegration`. Windows runs all platform-neutral tests
plus `WindowsIntegration`.

### P11.3 Artifact retention on failure

When native tests fail, upload:

- generated C;
- compiler logs;
- linker logs;
- intermediate diagnostic manifest.

Avoid uploading successful artifacts indefinitely unless needed.

### P11.4 Test representative fixtures

At minimum:

- simple US-like letters;
- AltGr Unicode mapping;
- ISO physical layout;
- special keys/extended scan code fixture.

## Acceptance criteria

Every commit that changes Windows translation/generation is validated by a real Windows native compilation job.

---

# Phase 12 — MVP stabilization and release readiness

## Objective

Turn the completed multi-target workflow into a coherent first distributable release.

## Work items

### P12.1 End-to-end scenario tests

Validate manually and/or with automation:

```text
New project
-> choose ISO-105
-> modify mappings
-> save
-> close/reopen
-> validate
-> select Windows target -> build and verify DLL
-> select Linux XKB target -> generate and verify symbols component
```

### P12.2 Error-path testing

Exercise:

- invalid project file;
- unknown schema version;
- missing target profile;
- unsupported target mapping;
- missing Windows toolchain;
- compiler failure;
- unavailable/failing XKB verifier;
- unwritable output path;
- cancelled build.

### P12.3 Documentation update

Update:

- README quick start;
- project format and target-profile documentation;
- Windows build prerequisites;
- Linux XKB generation, verification, and safe manual installation guidance;
- architecture diagrams if implementation diverged;
- limitations section.

### P12.4 Packaging the Avalonia application

Produce Windows and Linux desktop builds of KeyboardStudio. The Linux package must be able to generate
XKB text without development tools; `xkbcli` is an optional local verifier.

### P12.5 Versioning

Introduce application versioning and project schema version independently.

Example:

```text
KeyboardStudio app: 0.1.0
.kbdproj schema:    1
```

### P12.6 MVP exit criteria

The first MVP is complete only when all of the following are true:

- application opens on supported Windows and Linux versions;
- ISO-105 and ANSI-104 render correctly;
- four modifier layers can be edited;
- project save/load and target profiles are reliable;
- invalid projects produce actionable common and target-specific diagnostics;
- a valid project produces real Windows `KBDTABLES` source;
- Windows source compiles to a DLL with a supported toolchain;
- the output DLL passes structural/export verification;
- the same project produces a deterministic XKB v1 symbols component;
- the generated XKB component passes `xkbcli` verification on Linux CI;
- Linux and Windows integration CI are green;
- documentation matches behavior.

Implementation evidence and the remaining manual visual-release observations are tracked in
[`MVP-RELEASE-CHECKLIST.md`](MVP-RELEASE-CHECKLIST.md). CI exposes one final **MVP release gate** that
requires managed tests, Linux XKB integration, Windows native integration, and both self-contained
desktop package jobs for the same commit.

---

# Phase 13 — Linux focus and layout import

## Objective

Narrow the shipping user interface to the Linux XKB target, and remove the empty-keyboard starting
state by seeding every new document and letting the user list and import layouts already installed on
the host.

Design detail lives in [`LINUX-LAYOUT-IMPORT.md`](LINUX-LAYOUT-IMPORT.md); decisions in AD-019 to
AD-024.

## Work items

### P13.1 Embedded `us-basic` seed project

Add `templates/seeds/us-basic.kbdproj` as an embedded resource and make it the content of every new
document.

```text
KeyboardStudio.Core/Projects/Seeds/
  ISeedProjectSource.cs
  SeedProjectId.cs
  SeedProjectException.cs

KeyboardStudio.Persistence/Seeds/
  EmbeddedSeedProjectSource.cs
```

The seed is stored in the project file format, so the embedded source lives in
`KeyboardStudio.Persistence` and reads it through that assembly's DTOs and mapper; Core keeps only
the contract. `Create` is synchronous — a new document is built in a constructor, and the seed is an
embedded resource — and returns a fresh object graph per call so documents cannot share mapping
state.

The seed targets `iso-105` and maps all 105 keys: the alphanumeric block, digits, and punctuation
across `Default` and `Shift`, and every remaining key as a `Default`-layer special-key output. It
follows `us(basic)` for `<BKSL>` (backslash/bar) and the `pc` default for `<LSGT>` (less/greater),
which `us(basic)` does not define. It validates clean and translates to XKB with no diagnostics. On
`ansi-104` the mappings whose key is absent are dropped.

`scripts/generate-us-basic-seed.py` regenerates the seed from `templates/iso-105.json`; a test
asserts the seed's geometry matches that template key-for-key.

`DemoProjectFactory` is removed from `KeyboardStudio.Core` and becomes
`tests/Shared/TestProjectFactory.cs`, compiled into all four test assemblies. It stays a small
fixture rather than becoming the seed, so revising the seed cannot break unrelated tests. Tests whose
subject is unmapped-key behaviour inject an empty seed source instead of relying on what a new
document happens to contain.

Ships alone. No dependency on any other item in this phase.

### P13.2 Target visibility policy

```text
KeyboardStudio.App/Services/
  IBuildTargetVisibilityPolicy.cs
  EnvironmentBuildTargetVisibilityPolicy.cs
```

`BuildViewModel` filters `Targets` through the policy and exposes `IsTargetSelectorVisible`. When one
target is visible the selector is not rendered and the target name moves to a badge on the Build
card. `KEYBOARDSTUDIO_TARGETS=all` restores both.

`EnvironmentBuildTargetVisibilityPolicy` has a second constructor taking the override value directly,
so tests never mutate the process environment that xUnit shares across parallel collections.
`MainWindowViewModel` gained a constructor overload that accepts the policy; the shorter overloads
supply the environment-backed one. A policy that hid every target would leave a Build card with
nothing to build, so `BuildViewModel` falls back to the full list in that case.

Nothing is deleted. `KeyboardStudio.Windows` stays referenced and registered, `BuildTarget.WindowsX64`
stays in the enum, the `windowsX64` profile stays populated and persisted, and Windows CI is
untouched. Hidden profiles are still exported and reapplied by `ExportTargetProfiles` /
`ApplyTargetProfiles`, so a document authored on a Windows-enabled build round-trips unedited.

Ships alone.

### P13.3 Core import contract

```text
KeyboardStudio.Core/Layouts/Import/
  ILayoutImportSource.cs         ILayoutImportCatalog.cs      LayoutImportCatalog.cs
  ImportableLayoutDescriptor.cs  ImportableLayoutReference.cs LayoutSourceOrigin.cs
  LayoutImportOptions.cs         LayoutImportResult.cs        LayoutImportReport.cs
  LayoutImportFidelity.cs        LayoutImportDiagnostic.cs    LayoutImportDiagnosticCodes.cs
```

Identifiers are opaque strings; Core gains no XKB vocabulary. `LayoutImportDiagnostic` reuses
`ValidationSeverity` and adds `KeyId` and `ModifierLayer?`. Diagnostic codes use the `KSI` prefix,
registered in `LayoutImportDiagnosticCodes` beside the existing `KSL` XKB codes.

As built: the codes are declared in Core rather than in the assembly that raises them, unlike `KSL`,
because import loss is a property of the domain model and a second source would report the same
losses; their wording therefore names no file format. `LayoutImportReport.Classify` derives the
fidelity level so every source grades itself the same way, and `LayoutImportResult.Succeeded`/
`.Failed` make the `Success = true, Project = null` state unreachable. `LayoutImportCatalog` skips a
source that reports itself unavailable, propagates a failure from one that does not, and rejects
duplicate source IDs at registration since those IDs become document provenance.

### P13.4 XKB data roots and registry reader

```text
KeyboardStudio.Linux/Import/Discovery/   IXkbDataRootLocator, XkbDataRootLocator, XkbDataRoot
KeyboardStudio.Linux/Import/Registry/    IXkbLayoutRegistryReader, XkbRulesRegistryReader,
                                         XkbRegistryEntry
```

Roots resolve in libxkbcommon precedence order: `$XKB_CONFIG_ROOT`, then
`${XDG_CONFIG_HOME:-$HOME/.config}/xkb`, then `/etc/xkb`, then `/usr/share/X11/xkb` and
`/usr/local/share/X11/xkb`. The locator takes the environment and a filesystem abstraction as
constructor arguments so ordering is testable without touching the host.

`rules/evdev.xml` and `evdev.extras.xml` supply names, short descriptions, languages, and countries.
The file carries `<!DOCTYPE xkbConfigRegistry SYSTEM "xkb.dtd">`, so the reader **must** use
`XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }`. Layouts present in
`symbols/` but absent from the registry are still listed, with a `KSI010` informational diagnostic.

Ships a browsable catalog with no preview.

**As built.** The locator and reader sit over two host abstractions in
`KeyboardStudio.Linux/Import/Hosting/`: `IXkbEnvironment` and `IXkbFileSystem`, the latter exposing
no way to create or modify anything so import's read-only boundary is a property of the interface.
`$XKB_CONFIG_ROOT` outranks the user's directory and is tagged `System`; a relative
`XDG_CONFIG_HOME` is ignored per the base-directory specification; duplicate roots are listed once;
a host with no root yields an empty list rather than an error. The registry reader emits the bare
layout as a `VariantId = null` entry beside its variants, lets `evdev.xml` win over
`evdev.extras.xml` for a shared name, has variants inherit their layout's languages and countries
when they list none, skips entries with no `<name>`, treats a root with no registry as empty, and
lets a malformed registry throw rather than list nothing. `KSI010` for symbols-only layouts is
raised in P13.9, where the registry and symbols listings are unioned.

### P13.5 Symbols lexer and parser

```text
KeyboardStudio.Linux/Import/Symbols/  XkbSymbolsLexer, XkbSymbolsToken, XkbSymbolsTokenKind,
                                      XkbSymbolsParser, XkbSymbolsFile, XkbSymbolsSection,
                                      XkbSymbolsStatement (+ one file per derived statement)
```

The parser accepts the full statement vocabulary and consumes only what the model can hold: includes,
section flags (`default`, `partial`, `hidden`), `name[Group1]`, `key <NAME> { ... }`, and
`symbols[Group1]`. `type[GroupN]`, `key.type`, `modifier_map`, and `virtual_modifiers` are parsed and
ignored. Group2+, `actions[]`, `redirect`, and `overlay` produce `KSI020`/`KSI021` warnings. Unknown
statements skip to the next `;` with `KSI022` rather than aborting.

**As built.** The lexer never fails — unterminated strings and key names end at the line break, and
unknown characters become `Unknown` tokens — leaving every well-formedness judgement to the parser.
`Parse(path, text)` returns an `XkbSymbolsFile` carrying sections and findings together, with a
`DefaultSection` that resolves a bare include the way libxkbcommon does. Derived statements are
`XkbIncludeStatement`, `XkbNameStatement`, `XkbKeyStatement`, and `XkbIgnoredStatement`; the last
exists so "recognized and irrelevant" stays distinguishable from "not recognized", which is what
`KSI022` means. `XkbMergeMode` landed here rather than in P13.6 because the prefix sits on `key`
statements too, so the parser cannot represent `replace key <AD01>` without it. Statement skipping
counts braces so `modifier_map ... { ... };` cannot end its section early, while a key statement
claims only a terminator directly after its closing brace so a file that omits one does not lose the
next key. Over the installed 199-file corpus: 1,673 sections, 21,795 keys, 1,948 includes, 104
`KSI020`, 28 `KSI021`, and zero `KSI022` — a corpus test holds that last figure at zero.

### P13.6 Include resolution

```text
IXkbIncludeResolver, XkbIncludeResolver, XkbIncludeSpec, XkbMergeMode,
IXkbSymbolsResolver, XkbSymbolsResolver, ResolvedXkbSymbols, ResolvedXkbKey
```

Resolves `"file(section)"` and bare `"file"` (the file's `default` section) across the ordered roots,
including subdirectory forms such as `sun_vndr/us(sun_type6)`. Merge modes: default and `override`
let the includer win; `augment` keeps existing definitions; `replace` rebuilds the key; `alternate` is
treated as `override` with `KSI023` (six occurrences in the whole corpus).

**Cycle detection keys on `(resolved absolute path, section name)`, not on the file.** A file
legitimately includes other sections of itself — `pl(lefty)` includes `pl(basic)` — and a
file-granular visited set both breaks those layouts and hides real cycles. Depth cap 16, `KSI024`.

The resolved include chain is retained for the report.

**As built.** `XkbIncludeSpec` carries a merge mode and a target group, because an include string is
a merge expression: `+` composes with `override`, `|` with `augment`, and `:2` names a group. Group
2 and above is skipped with `KSI020` rather than flattened into group 1. A merge keyword can also
replace `include` outright — `augment "us(basic)"` — which the P13.5 parser dropped silently; that
is fixed here and pinned by a parser test.

An include that resolves to nothing needed a code, so `KSI025` (`CompositionTargetUnavailable`,
warning) was added to Core and `docs/DIAGNOSTICS.md`. It covers both a target no root holds and one
already being read, and the rest of the layout is still imported in either case.

`override` and `replace` differ only for a statement carrying no keysyms: `override` leaves existing
outputs alone, `replace` discards the definition. Keys come back in first-definition order, tracked
explicitly rather than relying on dictionary order. Parsed files are cached per resolution, failures
included. Every key records the `file(section)` that won.

Corpus check: all 1,673 sections of the host's 199 files resolve to 73,511 keys with zero `KSI025`
and zero `KSI024`, held there by an `XkbIntegration` test.

**Carried into P13.9.** `<LSGT>` is absent from `latin` and therefore from `pl(basic)` — it comes
from the `pc` component, which import does not compose. `XkbTemplateSelector` cannot key on `<LSGT>`
presence alone; the registry country hint needs to carry more weight than P13.9 assumes.

### P13.7 Keysym table and decoder

`scripts/generate-keysym-table` reads X.org `keysymdef.h` and libxkbcommon's legacy
keysym-to-Unicode table and emits a committed `XkbKeysymTable.g.cs` whose header records the upstream
source and version. CI regenerates and diffs. Both upstreams are permissively licensed (MIT / X11 /
HPND); attribution goes in the generated header and `templates/README.md`.

Neither `keysymdef.h` nor `xkbcommon-keysyms.h` can be assumed present at runtime, which is why the
table is generated at development time rather than read from the host.

`XkbKeysymDecoder` inverts `XkbKeysymMapper`: `NoSymbol`/`VoidSymbol` to `NoOutput`; `U0105` and
`0x01000105` forms and the direct Unicode/Latin-1 ranges to `CharacterOutput`; named keysyms through
the table; non-character function keys to `SpecialKeyOutput`; `dead_*` to `NoOutput` with `KSI031`;
anything unrecognized to `NoOutput` with `KSI032`.

**As built.** Five vendored sources rather than two, and several rules taken from libxkbcommon rather
than invented.

`scripts/generate-keysym-table.py` reads pinned copies under `third_party/keysyms` and emits
`XkbKeysymTable.g.cs`: 2,652 keysyms, 1,740 with a character. `keysymdef.h` alone was not enough —
the corpus test found `XF86*` in eleven files including `pc`, `Sun*` in `sun_vndr/`, `hp*` in
`hp_vndr/` and `apLineDel` in `digital_vndr/vt` — so `XF86keysym.h`, `Sunkeysym.h`, `HPkeysym.h` and
`ap_keysym.h` were added, each on that evidence. `DECkeysym.h` was not: nothing names a DEC keysym.
Over half of `XF86keysym.h` writes its value through an `_EVDEVK` macro, whose base is read from the
header rather than hard-coded. `HPkeysym.h` redefines `XK_Ydiaeresis` to something that is not
Y-with-diaeresis, so headers are read standard-first and later definitions are ignored and listed.
Where `keysym-utf.c` and a header disagree, libxkbcommon wins — it is what the user's machine
consults — and both current disagreements are listed in the generated header. Sources are vendored
rather than fetched so generation is reproducible offline and CI can diff without a network. CI runs
the script with `--check` in the managed build job. Attribution lives in the generated header and in
`third_party/keysyms/README.md` rather than `templates/README.md`, where the table does not belong.

The decoder's ordering is what makes it correct. Function keys are matched before the character
table, because `Return`, `Tab` and `KP_Multiply` all carry Unicode annotations upstream and would
otherwise import as control characters or a stray asterisk; letters and digits are deliberately not
function keys, or a Dvorak import would lose the layout. Four rules follow libxkbcommon's parser: `U`
takes one to eight hex digits and rejects `U+`; `any`, `none`, `nosymbol` and `voidsymbol` are empty
levels matched case-insensitively while every other name stays case-sensitive; `XF86_ClearGrab` is
the keysym `XF86ClearGrab`; and keysyms from `0x01000100` up are their character plus `0x01000000`,
a rule the generator also applies to seven unannotated aliases in `keysymdef.h`.

`XkbKeysymDecodeResult` gained an `XkbKeysymDecodeOutcome`. Three outcomes produce `NoOutput` and two
share `KSI032`, so without it a fidelity report could not distinguish a keysym the model has no place
for from text that names no keysym at all.

Corpus check: every keysym the host's 199-file corpus writes is recognised — 173,528 characters,
14,177 keys and 7,259 dead keys — with 505 distinct media, IME and vendor keysyms reported as
unrepresentable rather than dropped.

### P13.8 Bidirectional key-name tables

Add a table accessor to `IXkbKeyNameMapper` and derive `XkbKeyNameResolver` from the same data, so
generation and import cannot disagree about keys such as `<LSGT>`:

```csharp
public interface IXkbKeyNameMapper
{
    XkbKeyNameMappingResult Map(string templateId, string keyId);
    IReadOnlyDictionary<string, string> GetMappings(string templateId);
}
```

XKB keys with no template counterpart (`<I120>`, media keys, `<FK13>`+) are skipped with `KSI033` and
counted in `KeysSkipped`.

**As built.** The accessor and the resolver are as specified; the aliases were not anticipated.

Inverting the table is not enough on its own, because a key has as many names as the host's keycodes
file gives it and the table holds only the one generation writes. All forty-seven aliases
`keycodes/evdev` declares are transcribed into `XkbKeyNameResolver` and folded into the inverse table
in both directions and to a fixed point, an alias being a statement that two names share a keycode
rather than a redirection: `<I135>` reaches `<MENU>` only by way of `<COMP>`. Ten land on keys these
templates have; `<AC12>`, which fifteen files write for the backslash key, is the one that would have
been most visibly missed. Only evdev is transcribed — the Sun and Macintosh files alias keys neither
template has.

The phonetic `<LatA>`–`<LatZ>` aliases are ambiguous by construction. `keycodes/aliases` defines them
three times over and `rules/evdev` picks the section from the layout being loaded, so `<LatZ>` is
`<AB01>` for a US layout and `<AD06>` for a German one. Eight files write them, `symbols/de` among
them for phonetic Russian variants, so a single alias set would return those layouts with Y and Z
transposed. `XkbKeyAliasSet` names the three sets and `XkbKeyNameResolver.AliasSetForLayout` makes the
host's own choice from the layout name; a corpus test diffs both lists against `rules/evdev` so a
distribution moving a country between them is caught rather than followed silently.

`XkbKeyNameResolveResult` carries no outcome enum, unlike the keysym decoder's result: there is one
way to fail and one code to report it under. Its diagnostic carries no key id, the finding being that
there is no key to jump to.

Corpus check: 66,151 keys land on `iso-105` and 7,360 are skipped, every one of the latter a key no
PC keyboard has. A test asserts that nothing on the alphanumeric rows is skipped apart from the
Japanese, Brazilian and Sun extras, and two more check the alias tables against the host's own
`keycodes` files rather than against our reading of them.

### P13.9 Importer, template selection, fidelity report

`XkbLayoutImporter` and `XkbLayoutImportSource : ILayoutImportSource` assemble the project.

Levels map back as the inverse of generation: 1 to `Default`, 2 to `Shift`, 3 to `AltGr`, 4 to
`ShiftAltGr`, 5+ dropped with `KSI030`.

`XkbTemplateSelector` suggests geometry: `<LSGT>` present implies `iso-105`, otherwise `ansi-104`,
with registry country hints breaking ties. The suggestion is user-overridable because the registry
does not record physical geometry.

`KeyMapping.LogicalKey` is derived in order: the level-1 `SpecialKeyOutput`'s key; else the level-1
single ASCII letter or digit; else the template key ID's conventional logical key; else
`LogicalKey.None`. Rule three is what stops a Dvorak import from labelling every key by its produced
character instead of its physical identity.

**As built.** `Import/Translation/` holds `XkbLayoutImporter` and `XkbTemplateSelector`, and
`XkbLayoutImportSource` sits at `Import/`. The importer takes a flattened section and a registry hint and knows
nothing of where either came from, so the same code imports the host's database, a file the user
picked, and a fixture.

The importer puts the loaded template on the project rather than a bare reference to it. A project
whose mappings address keys its keyboard does not contain renders as an empty board, and a corpus
test now asserts that no imported mapping names a key its own geometry lacks.

A null variant resolves to the symbols file's `default` section rather than to a section named after
the word: `default` is a flag on a section called something else, usually `basic`, so passing the
word through as a name fails every bare layout — which is most of the catalog.

`XkbKeyNameResolver.AliasSetForLayout` is applied per import, so the resolver is composed once the
layout is known rather than injected ahead of it. Without it a phonetic layout written for a German
keyboard comes back with Y and Z transposed, which is the reason P13.8 built the alias sets.

The conventional logical-key table covers both templates completely and agrees with the `us-basic`
seed, the same convention written down as data. An earlier partial table omitted the twelve
punctuation keys, which is exactly where rule three does its work.

`KeysSkipped` counts a key whose every output was lost, not one the file deliberately left blank:
`NoSymbol` is a file saying "nothing here", and counting it as a loss would grade almost every
layout `Partial`. Levels that decode to nothing leave their layer unmapped rather than holding a
`NoOutput` the editor would render as blank anyway.

The catalog is the union of the registry and `symbols/`, keyed on the root that actually holds the
file so `Origin` follows the layout rather than being assumed. A registry entry no root implements
is not listed — `custom` is one the distribution ships for the user to write themselves — because
offering an entry that cannot be imported is a dead end. `KSI010` is raised at import rather than at
listing, `ListAsync` having no channel for findings.

`KSI034` (`TemplateNotAvailable`, error) was added to Core and `docs/DIAGNOSTICS.md` for a template
that cannot be loaded.

Corpus check: all 778 entries the host's catalog offers import, 37,300 keys in all, with no mapping
naming a key outside its own geometry.

### P13.10 Import dialog and provenance

```text
KeyboardStudio.App/Views/ImportLayoutDialog.axaml(.cs)
KeyboardStudio.App/ViewModels/  LayoutImportViewModel, ImportableLayoutViewModel,
                                LayoutImportReportViewModel
```

Entry points: **File > Import layout…**, an **Import…** button beside the existing
`New from [template] [Create]` control, and **File > Import from file…** for an arbitrary symbols
file. The dialog holds search, the grouped catalog list, variant selection, the geometry override, a
read-only `KeyControl` preview, and the fidelity summary, then commits as a new project or as a
mapping replacement that keeps geometry, target profiles, and file path.

`LayoutImportViewModel` depends only on `ILayoutImportCatalog`. Both commit paths route through the
existing unsaved-changes confirmation, and mapping replacement goes through `KeyboardEditor`.

`KeyboardProjectDocument` gains `importProvenance` (source, layout, variant, location, description,
timestamp) behind a `documentSchemaVersion` bump and a migration. Import pre-fills `XkbLayoutMetadata`
and always suffixes the layout ID (`pl` becomes `pl-custom`), never reusing the source ID: an artifact
named `symbols/pl` would shadow the distribution's own file if copied into an XKB root.

**As built.** `LayoutImportViewModel` reaches the platform only through `ILayoutImportCatalog`; its
other arguments are the geometry descriptors the application already has, not the provider that
produces them. `ImportableLayoutViewModel` groups the flat catalog by layout and
`ImportableVariantViewModel` is one choice inside it — the catalog arrives as one descriptor per
layout-and-variant pair, several hundred of them, which is the right shape to import from and the
wrong shape to choose from.

Selecting a layout imports it immediately: the import *is* the preview. A fidelity report the user
cannot see until after they commit is a report they cannot act on, and re-importing to commit would
risk showing one result and committing another. `PreviewTask` exposes the in-flight import so a
caller can wait for it; a newer selection cancels the older one.

The read-only preview is built from the editor's own `KeyViewModel` with no select command attached,
so it renders exactly what the editor will. A preview that is a second rendering can disagree with
the first, and the empty-keyboard defect P13.9 fixed was invisible until a project was drawn.

The geometry override is a checkbox plus a selector rather than a nullable selection, and the
suggestion is echoed back into the selector after each import so a user who takes it can still see
what they took. Choosing mapping replacement pins the geometry to the open document's own: that mode
keeps the document's keyboard, so a second geometry there would only invite keys that cannot fit.

`KeyboardEditor.ReplaceMappings` commits the replacement path, copying what it is given and
reporting how many mappings named a key the keyboard does not have. The commit decision lives in
`MainWindowViewModel` rather than in the dialog, and the unsaved-changes prompt runs after the
dialog rather than before it: both paths discard work in progress, and neither is worth prompting
about until the user has said which one they want.

`XkbSymbolsFileImportSource` (`linux-xkb-file`) serves **Import from file…**. It lists nothing and
imports the path on the reference, resolving that file under its own name — so a section it includes
from itself reaches the picked file rather than an installed layout of the same name — while its
other includes still come from the database. It is available only where a database is: symbols files
are written as differences, so without one an import yields the dozen keys the file overrides and a
report full of missing includes. `HostLayoutImportCatalog` registers both sources and is the only
place in the application that names a concrete one.

The envelope moved to `2` for `importProvenance`, with the `1`-to-`2` step registered even though it
changes nothing, so the chain has no gap for version 3 to fall through. `ProjectDocumentService`
owns provenance beside the current path and dirty flag, so every save carries it.

An imported document is not dirty and has no path, exactly like a new one: nothing has been written
yet and nothing changed since it was made. Replacement marks the document dirty, because it changed
one the user may already have saved.

### P13.11 Host layout detection and startup import

```text
IXkbActiveLayoutProbe, XkbActiveLayoutProbe, XkbActiveLayout
```

Detection order: `XKB_DEFAULT_LAYOUT`/`XKB_DEFAULT_VARIANT`, then `Option "XkbLayout"`/`"XkbVariant"`
in `/etc/X11/xorg.conf.d/00-keyboard.conf`, then `KEYMAP=` in `/etc/vconsole.conf`, then `us`. File
and environment reads only — no process is spawned.

The seed loads first so the first frame never waits on the filesystem; the host import runs
asynchronously and replaces the seed only while the document is still pristine. Failure is silent
apart from a diagnostics entry.

### P13.12 Import test coverage

Golden imports of vendored, pinned `us`/`pl`/`de`/`fr` fixtures; a full import to generation to
re-import round trip; a Linux CI soak importing every layout and variant the host advertises; and an
`xkbcli` conformance oracle skipped when the tool is absent.

## Tests

`KeyboardStudio.Core.Tests`

- the seed project deserializes, validates clean, and matches its declared template;
- catalog aggregation, source ordering, and unavailable-source handling.

`KeyboardStudio.Linux.Tests`

- lexer and parser: every statement kind, comments, malformed input, unterminated sections;
- include resolver: default/`augment`/`replace`/`alternate`, cross-file, subdirectory,
  self-referencing sections, genuine cycles, depth cap;
- registry reader: DTD not resolved, malformed XML, missing variant lists, user/system merge;
- decoder symmetry: exhaustive round trip against `XkbKeysymMapper`;
- key-name resolver symmetry against `XkbKeyNameMapper` for both templates;
- template selection from `<LSGT>` presence and country hints;
- golden imported projects snapshot-compared as JSON;
- `[Trait("Category", "XkbIntegration")]` soak and `xkbcli` oracle.

`KeyboardStudio.App.Tests`

- new documents are non-empty and validate clean;
- default policy hides the target selector and edits the Linux profile; a loaded Windows profile
  survives save and reload unedited; `KEYBOARDSTUDIO_TARGETS=all` restores both targets;
- catalog listing, filtering, variant and geometry selection, fidelity presentation;
- import as new project versus replace mappings, including the unsaved-changes path;
- startup fallback chain: env, then `00-keyboard.conf`, then `vconsole.conf`, then `us`, then seed on
  failure.

## Acceptance criteria

- no user action produces a document with zero mappings;
- the build panel shows no target selector and no Windows-specific field, while
  `KeyboardStudio.Windows.Tests` and the Windows CI job remain green and unmodified;
- a saved document authored before this phase reloads with its `windowsX64` profile byte-identical;
- the import dialog lists the host's layouts and variants, grouped by user and system origin, and
  filters by name, ID, language, and country;
- importing `pl(basic)` on a stock xkeyboard-config yields a validating project whose four layers
  match the source for every key the model can represent, with each dropped dead key reported against
  its key and layer;
- import, generate, re-import returns an equal Core model for `us(basic)`;
- a missing, empty, or malformed XKB database degrades to the seed without an unhandled exception;
- no import path writes to any XKB root.

---

## 5. Cross-cutting technical work

### 5.1 Logging

Use structured application logging behind an abstraction or standard .NET logging interface.

Log:

- project load/save failures;
- selected target/profile and validation summary;
- target-specific generation stages;
- compiler commands with sensitive/path considerations;
- compiler/linker exit codes;
- XKB verifier commands and exit codes;
- artifact verification and output hashes.

Do not make logs the only place users can discover build failures.

### 5.2 Error model

Prefer structured result types/exceptions at subsystem boundaries.

Examples:

```text
ProjectLoadException
ProjectValidationResult
SourceGenerationException
BuildEnvironmentStatus
KeyboardBuildResult
ArtifactResult
ArtifactVerificationResult
```

Avoid returning `null` to represent operational failure.

### 5.3 Cancellation

All potentially slow operations should accept `CancellationToken`:

- project I/O;
- source generation if it becomes significant;
- native compile/link;
- external XKB verification;
- artifact verification.

### 5.4 Immutability boundaries

Physical templates should be treated as immutable after loading.

Project mappings may remain mutable behind `KeyboardEditor`, but mutation should not leak freely through ViewModels.

### 5.5 Threading

Build, generation, verifier, and file operations must not block the Avalonia UI thread.

Only UI state updates should return to the UI scheduler.

### 5.6 Security

The application invokes native compilers and emits configuration files consumed by system tools, so:

- never construct shell commands from unvalidated project names;
- sanitize generated filenames/identifiers;
- use argument-list process APIs;
- keep build output inside controlled directories;
- do not execute generated output DLLs as arbitrary code;
- do not write into a user's or system's active XKB configuration during generation or verification;
- treat imported project files as untrusted data.

---

## 6. Testing pyramid

### Unit tests — majority

Cover:

- domain edits;
- validation;
- DTO mapping;
- template parsing;
- Windows semantic translation;
- XKB physical-key and keysym translation;
- C generation helpers;
- XKB symbols generation;
- target backend resolution;
- build command construction;
- ViewModel state transitions.

### Golden/source tests

Cover deterministic generated Windows source and Linux XKB symbols components.

Golden fixtures should be few, representative, and intentionally reviewed when changed.

### Integration tests

Cover:

- JSON files on disk;
- generated XKB compilation with `xkbcli` on Linux;
- Windows compiler/toolchain discovery;
- generated C compilation;
- DLL export verification.

### Manual exploratory tests

Cover:

- keyboard editing ergonomics;
- display scaling;
- file dialogs;
- toolchain setup failures;
- Windows desktop behavior;
- Linux desktop behavior and manual XKB import guidance.

---

## 7. Proposed milestone grouping

### Milestone A — Usable editor core

Includes phases 0-4.

Result:

- real keyboard templates;
- visual editing;
- save/load;
- diagnostics;
- no real native DLL yet.

### Milestone B — Native source correctness

Includes phases 5-6.

Result:

- full Windows intermediate model;
- real `KBDTABLES` C source;
- strong golden tests;
- compilation may still be external/manual.

### Milestone C — Native build pipeline

Includes phases 7-8.

Result:

- toolchain detection;
- automated compile/link;
- verified DLL artifact.

### Milestone D — Multi-target artifacts

Includes phase 9.

Result:

- target-based backend dispatch;
- deterministic XKB symbols generation;
- Linux `xkbcli` verification and CI coverage.

### Milestone E — Productized MVP

Includes phases 10-12.

Result:

- target-aware GUI build workflow;
- Windows CI;
- stabilized Windows and Linux end-to-end application.

---

## 8. Recommended commit strategy

Keep commits small enough to review and bisect.

Examples:

```text
Add project persistence DTOs
Add ISO-105 keyboard template
Render keyboard using physical geometry
Add project document service
Add mapping validation rules
Add Windows virtual-key translation
Generate Windows modifier tables
Generate VK_TO_WCHARS tables
Generate KBDTABLES descriptor
Detect MSVC/WDK toolchain
Compile generated keyboard source
Verify keyboard DLL exports
Resolve build backend by selected target
Map template keys to XKB key names
Generate deterministic XKB symbols component
Verify generated XKB with xkbcli
Add Windows native build CI
```

Avoid commits that mix UI redesign, persistence changes, and Windows ABI work unless they are inseparable.

---

## 9. Definition of done for each work item

A work item is done when:

1. production implementation is complete;
2. relevant automated tests exist and pass;
3. existing tests remain green;
4. public/internal API names are understandable;
5. architecture boundaries remain intact;
6. failure behavior is defined;
7. documentation is updated if the behavior or format changed;
8. CI validates the change on every applicable platform.

---

## 10. Risk register

### Risk R1 — Windows keyboard ABI assumptions

**Risk:** generated tables compile but do not behave correctly in Windows.

**Mitigation:** use Microsoft sample layouts as structural references, keep a typed intermediate model, add Windows integration fixtures, and verify incrementally rather than generating all structures at once.

### Risk R2 — AltGr semantics

**Risk:** treating AltGr as a simple independent modifier may produce incorrect Ctrl/Alt behavior.

**Mitigation:** isolate modifier translation in each target backend. Test Windows modifier-number tables
against known layouts and XKB levels 3/4 against `xkbcli`-compiled fixtures.

### Risk R3 — Scan-code/virtual-key confusion

**Risk:** mixing physical scan codes and logical virtual keys creates hard-to-debug layout errors.

**Mitigation:** retain separate types/models and explicit translation tables; never store Windows VK values in `PhysicalKey`.

### Risk R4 — Persistence coupled to domain implementation

**Risk:** domain refactoring breaks old `.kbdproj` files.

**Mitigation:** persistence DTOs + schema migration pipeline.

### Risk R5 — Toolchain discovery fragility

**Risk:** hard-coded Visual Studio/WDK paths fail across machines.

**Mitigation:** centralize discovery and test multiple installed-toolchain scenarios.

### Risk R6 — Generated source becomes unreadable

**Risk:** generator evolves into opaque string concatenation.

**Mitigation:** structured emitter/helpers, deterministic formatting, golden tests, source comments naming generated table sections.

### Risk R7 — UI becomes coupled to mutable domain graph

**Risk:** dirty tracking, undo/redo, and validation become difficult.

**Mitigation:** keep mutations behind `KeyboardEditor`/document services and use ViewModels for orchestration only.

### Risk R8 — Cross-platform editor accidentally becomes Windows-only

**Risk:** build-toolchain code leaks into App startup or Core.

**Mitigation:** keep Windows build services replaceable and expose unavailable build environment cleanly on non-Windows hosts.

### Risk R9 — Physical key identity differs by platform

**Risk:** treating a Windows scan code as an XKB key name produces incorrect ISO/ANSI mappings,
especially for extended, keypad, and international keys.

**Mitigation:** use stable template key IDs as the common identity and maintain explicit,
backend-owned mappings to Windows scan codes and XKB symbolic key names. Reject unknown pairs.

### Risk R10 — A compiler-shaped pipeline distorts text artifacts

**Risk:** forcing XKB generation through `INativeCompiler` creates fake environments, misleading build
states, and platform checks that prevent portable generation.

**Mitigation:** resolve an `IBuildBackend` by target. Each backend owns its stages; only Windows uses
compile/link, while Linux materializes XKB text and optionally invokes a verifier.

### Risk R11 — Generated XKB changes the active desktop configuration

**Risk:** tests or builds overwrite system/user XKB files or activate an invalid layout.

**Mitigation:** generate and verify only in isolated workspaces. Installation and activation require a
separate, explicit post-MVP workflow and are never performed by normal tests.

---

## 11. Explicitly deferred after MVP

The following should be planned only after the direct-mapping workflow is stable:

- dead keys;
- chained dead keys;
- ligatures/multi-character output;
- compose sequences;
- locale-specific advanced behavior;
- installation/registry registration;
- automatic XKB installation, desktop registration, or activation;
- uninstall/upgrade of installed layouts;
- importing existing `.klc` projects;
- importing/decompiling layout DLLs;
- custom physical keyboard template editor;
- macros/scripts;
- IMEs;
- runtime remapping/hooking;
- macOS artifact backends.

The architecture should not block these features, but MVP code should not pay their complexity cost yet.

---

## 12. Immediate next implementation order

Phases 0-12 are complete. The recommended next work is:

1. **Ship the `us-basic` seed so no document opens empty (P13.1).** No dependencies; fixes the
   starting-state problem on its own.
2. **Apply the target visibility policy and narrow the build panel to XKB (P13.2).** Independent of
   the importer.
3. **Build the catalog: Core contract, data-root discovery, registry reader (P13.3-P13.4).** Ends
   with a browsable, previewless layout list.
4. **Build the resolver: lexer, parser, include resolution (P13.5-P13.6).**
5. **Build the translators: keysym table and decoder, bidirectional key names (P13.7-P13.8).**
6. **Assemble the importer and the import dialog (P13.9-P13.10).**
7. **Detect and import the host layout at startup (P13.11).**
8. **Close with golden, round-trip, soak, and oracle coverage (P13.12).**

Each of items 1, 2, 3, 6, and 7 is independently shippable, so the empty-keyboard fix and the
Linux-only UI land long before the parser is finished.
