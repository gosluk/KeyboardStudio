# KeyboardStudio Implementation Plan

## 1. Purpose

This document is the executable implementation plan for KeyboardStudio. It starts from the current `architecture` branch state, where the solution, Avalonia shell, core domain model, JSON persistence skeleton, Windows source-generation skeleton, tests, and CI already compile successfully.

The goal is to move from that bootstrap state to a usable first release that can:

1. display realistic ISO/ANSI physical keyboard templates;
2. edit key mappings for `Default`, `Shift`, `AltGr`, and `Shift+AltGr`;
3. create, save, load, and validate `.kbdproj` projects;
4. translate the platform-neutral project into real Windows keyboard tables;
5. compile those tables into a native Windows keyboard-layout DLL;
6. expose the build process from the Avalonia application with useful diagnostics;
7. verify the produced DLL through automated tests and Windows CI.

The plan intentionally keeps installation/registry registration, dead keys, ligatures, macros, IMEs, and runtime remapping outside the first release.

---

## 2. Current baseline

The current source skeleton already provides:

- `KeyboardStudio.sln` targeting .NET 10;
- `KeyboardStudio.App` using Avalonia;
- `KeyboardStudio.Core` with keyboard/project domain objects;
- `KeyboardStudio.Persistence` with JSON persistence;
- `KeyboardStudio.Build` with build abstractions/orchestration;
- `KeyboardStudio.Windows` with a deterministic source-generation skeleton;
- Core and Windows test projects;
- placeholder ISO-105 and ANSI-104 template files;
- GitHub Actions restore/build/test validation;
- a minimal editor window that renders demo keys and changes outputs.

The most important missing functionality is the real Windows backend. The existing `WindowsArtifactGenerator` is a structural placeholder and does not yet emit the complete `KBDTABLES` implementation required by Windows.

---

## 3. Delivery principles

### 3.1 Keep Core platform-neutral

`KeyboardStudio.Core` must never acquire references to:

- Avalonia;
- Win32 APIs;
- WDK headers;
- `kbd.h` structures;
- MSVC command-line details;
- registry installation code.

Windows-specific knowledge belongs in `KeyboardStudio.Windows` and toolchain execution belongs in `KeyboardStudio.Build`.

### 3.2 Prefer deterministic transformations

Every important transformation should be a pure or near-pure function:

```text
KeyboardProject
    -> validation result
    -> Windows intermediate model
    -> generated source files
    -> compiler invocation
    -> build result
```

The same project and options must produce byte-for-byte identical generated source.

### 3.3 Add behavior behind tests first

Each phase below has a test gate. A phase is not considered complete merely because the UI appears to work.

### 3.4 Avoid premature advanced layout features

Version 1 supports only direct character outputs and special logical keys across four modifier layers. Dead keys and composition features must not distort the first implementation.

### 3.5 Preserve readable generated C

The generated Windows source is both an intermediate artifact and a debugging tool. It should be human-readable, stable, and easy to compare against Microsoft's keyboard-layout samples.

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
Phase 9  Build UX
   |
   v
Phase 10 Windows integration CI
   |
   v
Phase 11 MVP stabilization and release readiness
```

Phases 1-4 can be developed partly in parallel with early research/prototyping for phases 5-7, but the Windows compiler implementation must not force Windows concepts into the core model.

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

Initially support:

- x64;

Then add if straightforward:

- ARM64;
- x86 only if still worth supporting for the target Windows versions.

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

# Phase 9 — Build user experience

## Objective

Expose the Windows build pipeline cleanly in Avalonia.

## Work items

### P9.1 Build panel

Display:

- selected target architecture;
- toolchain availability;
- validation status;
- output directory;
- Build button;
- build progress state;
- final artifact path.

### P9.2 Disable invalid actions

Build is disabled when:

- project has blocking validation errors;
- no compatible Windows toolchain is available;
- a build is already running.

### P9.3 Build diagnostics

Show stages:

```text
Validating
Generating source
Compiling
Linking
Verifying
Completed / Failed
```

### P9.4 Open generated files/output

Provide actions to:

- open output directory;
- inspect generated source;
- copy diagnostic/build log.

### P9.5 Error presentation

Distinguish:

- project validation error;
- source-generation error;
- missing toolchain;
- compiler error;
- linker error;
- artifact verification error.

## Tests

- Build command enablement;
- state transitions;
- cancellation;
- success/failure result presentation;
- validation errors prevent compiler invocation.

## Acceptance criteria

A Windows user can build a valid project from the GUI and understand failures without reading application source code.

---

# Phase 10 — Windows integration CI

## Objective

Continuously prove that generated source actually compiles on Windows.

## Work items

### P10.1 Add Windows runner

Extend GitHub Actions with a Windows job that:

1. restores;
2. builds .NET solution;
3. runs all unit tests;
4. locates the Windows build toolchain;
5. builds one or more fixture keyboard DLLs;
6. verifies exports/artifacts.

### P10.2 Separate fast and native tests

Use categories/traits:

```text
Unit
Golden
WindowsIntegration
```

Ubuntu should continue running all platform-neutral tests.

Windows runs everything plus native integration tests.

### P10.3 Artifact retention on failure

When native tests fail, upload:

- generated C;
- compiler logs;
- linker logs;
- intermediate diagnostic manifest.

Avoid uploading successful artifacts indefinitely unless needed.

### P10.4 Test representative fixtures

At minimum:

- simple US-like letters;
- AltGr Unicode mapping;
- ISO physical layout;
- special keys/extended scan code fixture.

## Acceptance criteria

Every commit that changes Windows translation/generation is validated by a real Windows native compilation job.

---

# Phase 11 — MVP stabilization and release readiness

## Objective

Turn the completed core workflow into a coherent first distributable release.

## Work items

### P11.1 End-to-end scenario tests

Validate manually and/or with automation:

```text
New project
-> choose ISO-105
-> modify mappings
-> save
-> close/reopen
-> validate
-> generate source
-> build DLL on Windows
-> verify artifact
```

### P11.2 Error-path testing

Exercise:

- invalid project file;
- unknown schema version;
- missing toolchain;
- compiler failure;
- invalid Windows mapping;
- unwritable output path;
- cancelled build.

### P11.3 Documentation update

Update:

- README quick start;
- project format documentation;
- Windows build prerequisites;
- architecture diagrams if implementation diverged;
- limitations section.

### P11.4 Packaging the Avalonia application

Produce at minimum a Windows desktop build of KeyboardStudio.

Cross-platform editor packaging can follow once Windows DLL compilation behavior is stable.

### P11.5 Versioning

Introduce application versioning and project schema version independently.

Example:

```text
KeyboardStudio app: 0.1.0
.kbdproj schema:    1
```

### P11.6 MVP exit criteria

The first MVP is complete only when all of the following are true:

- application opens on supported Windows version;
- ISO-105 and ANSI-104 render correctly;
- four modifier layers can be edited;
- project save/load is reliable;
- invalid projects produce actionable diagnostics;
- valid project produces real Windows `KBDTABLES` source;
- source compiles to a DLL with supported Windows toolchain;
- output DLL passes structural/export verification;
- Ubuntu platform-neutral CI is green;
- Windows native integration CI is green;
- documentation matches behavior.

---

## 5. Cross-cutting technical work

### 5.1 Logging

Use structured application logging behind an abstraction or standard .NET logging interface.

Log:

- project load/save failures;
- validation summary;
- source generation stages;
- compiler commands with sensitive/path considerations;
- compiler/linker exit codes;
- artifact verification.

Do not make logs the only place users can discover build failures.

### 5.2 Error model

Prefer structured result types/exceptions at subsystem boundaries.

Examples:

```text
ProjectLoadException
ProjectValidationResult
SourceGenerationException
BuildEnvironmentStatus
CompilationResult
ArtifactVerificationResult
```

Avoid returning `null` to represent operational failure.

### 5.3 Cancellation

All potentially slow operations should accept `CancellationToken`:

- project I/O;
- source generation if it becomes significant;
- native compile/link;
- artifact verification.

### 5.4 Immutability boundaries

Physical templates should be treated as immutable after loading.

Project mappings may remain mutable behind `KeyboardEditor`, but mutation should not leak freely through ViewModels.

### 5.5 Threading

Native build and file operations must not block the Avalonia UI thread.

Only UI state updates should return to the UI scheduler.

### 5.6 Security

The application invokes native compilers, so:

- never construct shell commands from unvalidated project names;
- sanitize generated filenames/identifiers;
- use argument-list process APIs;
- keep build output inside controlled directories;
- do not execute generated output DLLs as arbitrary code;
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
- C generation helpers;
- build command construction;
- ViewModel state transitions.

### Golden/source tests

Cover deterministic generated Windows source.

Golden fixtures should be few, representative, and intentionally reviewed when changed.

### Integration tests

Cover:

- JSON files on disk;
- Windows compiler/toolchain discovery;
- generated C compilation;
- DLL export verification.

### Manual exploratory tests

Cover:

- keyboard editing ergonomics;
- display scaling;
- file dialogs;
- toolchain setup failures;
- Windows desktop behavior.

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

### Milestone D — Productized MVP

Includes phases 9-11.

Result:

- GUI build workflow;
- Windows CI;
- stabilized end-to-end application.

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

**Mitigation:** isolate AltGr translation entirely in the Windows backend and test modifier-number tables against known layouts.

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

---

## 11. Explicitly deferred after MVP

The following should be planned only after the direct-mapping workflow is stable:

- dead keys;
- chained dead keys;
- ligatures/multi-character output;
- compose sequences;
- locale-specific advanced behavior;
- installation/registry registration;
- uninstall/upgrade of installed layouts;
- importing existing `.klc` projects;
- importing/decompiling layout DLLs;
- custom physical keyboard template editor;
- macros/scripts;
- IMEs;
- runtime remapping/hooking;
- macOS/Linux artifact backends.

The architecture should not block these features, but MVP code should not pay their complexity cost yet.

---

## 12. Immediate next implementation order

The recommended next commits from the current skeleton are:

1. **Add persistence DTOs and schema-version handling.**
2. **Implement complete ISO-105 template parsing and data.**
3. **Render keyboard using physical geometry and reusable `KeyControl`.**
4. **Add project document lifecycle and dirty tracking.**
5. **Complete mapping editor and validation diagnostics.**
6. **Introduce typed Windows virtual-key and modifier intermediate model.**
7. **Generate real scan-code and modifier tables.**
8. **Generate real `VK_TO_WCHARS` and `KBDTABLES` source.**
9. **Add Windows native compile/link integration.**
10. **Add DLL verification and Windows CI.**
11. **Expose complete build workflow in Avalonia.**
12. **Run end-to-end stabilization and prepare MVP release.**

This ordering maximizes early usable editor functionality while keeping the Windows ABI work isolated, testable, and incremental.
