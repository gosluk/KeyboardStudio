# Initial Architecture Decisions

This file records the architectural decisions that should remain stable while the first implementation is developed.

## AD-001 - Avalonia is a presentation layer only

The core project is UI-framework independent. Avalonia objects must not be referenced by the domain model.

## AD-002 - Platform-neutral project model

A `.kbdproj` describes physical keys, logical mappings and modifier outputs without serializing Windows `KBDTABLES` or other native structures.

## AD-003 - JSON project persistence

Projects use versioned JSON with a dedicated `.kbdproj` extension. `schemaVersion` is mandatory from version 1.

## AD-004 - Physical geometry uses templates

Standard keyboard geometry is stored in reusable templates such as ISO-105 and ANSI-104. Projects reference a template rather than duplicating geometry.

## AD-005 - Native Windows source is generated directly

KeyboardStudio is intended to generate native Windows keyboard layout source rather than using MSKLC as a required build dependency.

## AD-006 - Source generation and compilation are separate

`WindowsCSourceGenerator` can be tested without MSVC/WDK. `INativeCompiler` owns actual native process execution.

## AD-007 - Initial modifier scope is intentionally limited

The first release supports `Default`, `Shift`, `AltGr`, and `ShiftAltGr`. More advanced modifier states, dead keys and ligatures are deferred.

## AD-008 - All editing mutations pass through KeyboardEditor

ViewModels orchestrate UI state but do not directly mutate arbitrary nested project state. This leaves room for validation, dirty tracking and undo/redo.

## AD-009 - General and target metadata are separate

`ProjectMetadata` contains only cross-platform information: display name, description, user-managed
project version, and language/locale. Windows layout identity is represented by
`WindowsLayoutMetadata` in `KeyboardStudio.Windows`; Linux layout/section identity is represented by
`XkbLayoutMetadata` in the planned `KeyboardStudio.Linux`. Neither belongs in `KeyboardStudio.Core`.

Persistence DTOs must not solve target metadata by making `KeyboardStudio.Persistence` depend on a
platform backend or by putting backend fields into the core aggregate. The current
`IKeyboardProjectStore` transports only the platform-neutral `KeyboardProject`; target-specific
document/settings persistence must be introduced through a boundary that can preserve both profiles
without reversing dependency direction.

## AD-010 - Persistence DTOs own the wire contract

`JsonKeyboardProjectStore` serializes persistence DTOs and maps them explicitly to and from the domain model. JSON attributes, wire discriminators and persistence-specific enum names belong in `KeyboardStudio.Persistence`, not in `KeyboardStudio.Core`.

This allows the domain model to evolve independently while schema migrations and wire-format compatibility remain explicit persistence responsibilities.

## AD-011 - Document lifecycle is an application concern

`IProjectDocumentService` in `KeyboardStudio.App` owns New/Open/Save/Save As semantics, the current project path, document dirty state, and translation of expected persistence or file-system failures into presentation-safe errors.

Avalonia storage pickers are responsible only for choosing paths. `KeyboardStudio.Persistence` continues to serialize streams and does not acquire UI or file-dialog dependencies. Editor-to-dirty-state wiring and unsaved-change prompts remain part of the later editor lifecycle work.

## AD-012 - Project migrations transform persistence JSON before DTO mapping

Project schema migrations live in `KeyboardStudio.Persistence` and operate on `JsonObject` documents before the current persistence DTO is deserialized. `JsonKeyboardProjectStore` is responsible for schema validation and delegates legacy upgrades to `ProjectMigrationPipeline` rather than accumulating version-specific switch logic.

Each `IProjectMigration` advances exactly one schema version. The pipeline applies registered migrations in order, stamps `schemaVersion` after each successful step, and fails explicitly when a required step is missing. Schema version 1 remains the first version, so no synthetic v0 migration is introduced.

## AD-013 - Windows semantic translation is explicit and complete before generation

`WindowsLayoutTranslator` converts every supported logical key through an explicit mapping to a
Windows virtual key. It produces separate normal and extended scan-code collections, an eight-state
Windows modifier-number table, and typed character rows before any C source is generated.

AltGr uses the Windows Ctrl+Alt bit relationship. Scan-only logical keys do not participate in the
character table. The v1 character model supports BMP values that fit one native `WCHAR`; non-BMP
characters and layer-specific special-key remaps are rejected with structured diagnostics until
ligature or broader special-key support is implemented.

## AD-014 - Native Windows source mirrors the minimal WDK keyboard-layout ABI

The Windows generator produces a deterministic four-file set named `keyboard.c`, `keyboard.h`,
`keyboard.def`, and `keyboard.rc`. The stable generic names simplify the compiler working directory;
layout identity belongs in generated comments, module/resource metadata, and the eventual DLL name.

The C translation unit uses numeric virtual-key and UTF-16 values with WDK flags and sentinel rows.
Optional dead-key, ligature, and locale-specific structures remain explicit null/zero `KBDTABLES`
fields until their semantic models exist. Source files contain no generated timestamps or host paths.

## AD-015 - Native builds use discovered tools and isolated disposable workspaces

Windows toolchain discovery prefers the active Visual Studio developer environment, then `vswhere`
for MSVC and the Windows Kits registry for the SDK/WDK. A resolved environment contains exact tool,
include, library, architecture, and version data; no repository-relative compiler paths are assumed.

Every native build writes generated files, objects, outputs, and logs below a unique workspace. The
default cleanup policy removes successful-build intermediates but retains the DLL and raw log, while
failed and cancelled builds retain their diagnostic workspace. Callers may retain all files or delete
failed workspaces explicitly through `BuildCleanupPolicy`.

## AD-016 - Build orchestration resolves one backend by artifact target

`BuildOrchestrator` owns common project validation and resolves exactly one `IBuildBackend` from
`BuildOptions.Target`. The selected backend owns target compatibility validation, generation,
materialization, verification, and its environment status.

Windows backends retain `IArtifactGenerator`, `IBuildEnvironment`, and `INativeCompiler` as internal
collaborators. The Linux XKB backend writes its generated text as the final artifact and must not use a
no-op native compiler. Results and UI stages use target-neutral artifact terminology at the backend
boundary.

## AD-017 - The Linux artifact is an XKB v1 symbols component

The Linux backend generates classic XKB text format v1 at `symbols/<layout-id>`. It emits an
`xkb_symbols` component rather than a self-contained keymap so the artifact composes with the host's
standard keycodes, types, compatibility data, and rules. V1 is chosen for X11 and Wayland interchange
compatibility.

Generation is deterministic managed code and does not require Linux or libxkbcommon. When available,
`xkbcli compile-keymap --test` verifies the component in an isolated include root; Linux CI requires
that verification. Normal build and test workflows never install or activate the layout.

## AD-018 - Platform physical identities are mapped from stable template key IDs

`(PhysicalKeyboard.Id, PhysicalKey.Id)` is the shared physical identity at translation boundaries.
The Windows backend consumes the template's scan-code data, while the Linux backend uses explicit
ISO-105/ANSI-104 tables to map stable key IDs to XKB symbolic names such as `<AC01>` and `<LSGT>`.

XKB names must not be inferred from Windows scan codes or stored in Core. Unknown template/key pairs
fail with structured, key-linked target diagnostics.

## AD-019 - Layout import is a target-neutral Core contract with platform sources

`KeyboardStudio.Core` defines `ILayoutImportSource` and `ILayoutImportCatalog` over opaque
source/layout/variant identifiers. Every XKB parser, resolver, and table lives in
`KeyboardStudio.Linux/Import/`, and ViewModels see only the catalog.

Import produces a `KeyboardProject`, so the contract belongs in Core; naming layouts by opaque strings
keeps Core free of XKB vocabulary under AD-002 and architecture 2.1. A future Windows `.klc` or
installed-DLL source implements the same interface without reshaping the editor.

## AD-020 - XKB import uses a managed parser, not `xkbcli`

Import lexes, parses, and resolves `xkb_symbols` includes in managed code. `xkbcli` stays an optional
CI conformance oracle whose resolved key/level tables are diffed against the managed resolver.

`xkbcli compile-keymap` would return a flat resolved keymap and remove the need for an include
resolver, but libxkbcommon-tools is absent from most desktop installs, and AD-017 already fixes
`xkbcli` as an optional verifier that never produces a result. Depending on it at runtime would
invert that and make import non-deterministic across hosts. The corpus makes a managed resolver
affordable: of 1933 include statements in xkeyboard-config 2.47, all but six use the default merge
mode, and `Group2` appears in two statements.

## AD-021 - Import is lossy by design and reports every loss

Dead keys, groups beyond the first, levels beyond four, XKB actions, and unmappable keysyms are
dropped with key- and layer-linked diagnostics. They never fail the import.

The purpose of import is a usable starting point for editing. Refusing every layout that uses a
`dead_*` keysym would reject most European layouts, including the ones the feature exists to serve.
`LayoutImportReport` carries the fidelity level, counts, resolved include chain, and diagnostics, and
the import dialog shows it before the project is replaced.

## AD-022 - XKB key-name tables are bidirectional and single-sourced

`XkbKeyNameMapper` owns one `(templateId, keyId) -> XKB name` table and exposes it. Generation reads
it forward and import reads it inverted.

Two independently maintained tables would drift, and the first disagreement would be silent: an
exported layout that no longer re-imports to the same model. XKB names stay out of Core and are still
never inferred from `PhysicalKey.ScanCode`, preserving AD-018.

## AD-023 - A new document is never empty

An embedded `us-basic` seed project is the content of every new document. On Linux the host's
configured layout is imported over it asynchronously while the document is still pristine.

Bare geometry with zero mappings is not a usable starting point. The seed is host-independent so the
guarantee holds on every platform, including hosts with no XKB data. Host detection reads
`XKB_DEFAULT_LAYOUT`, `/etc/X11/xorg.conf.d/00-keyboard.conf`, then `/etc/vconsole.conf`, and spawns
no process; `localectl` and `gsettings` write those same files and would add a process dependency to
the startup path. A failed host import degrades to the seed rather than blocking the editor.

The seed is stored in the project file format, so `EmbeddedSeedProjectSource` lives in
`KeyboardStudio.Persistence` and reuses that assembly's DTOs and mapper; only the contract lives in
Core. A seed parser inside Core would be a second implementation of the same format, free to drift
from the one that reads user files. The seed's geometry is generated from the `iso-105` template and
tested against it for the same reason.

`DemoProjectFactory` is removed from `KeyboardStudio.Core`. It shipped a fixture in the production
assembly; the tests that used it now compile `tests/Shared/TestProjectFactory.cs`, which is
deliberately not the seed — a fixture that tracked the seed would make seed edits break unrelated
tests.

## AD-024 - Target visibility is presentation-only and reversible

The shipping UI exposes `LinuxXkb` and hides `WindowsX64`. Hiding is enforced solely by
`IBuildTargetVisibilityPolicy` in the application layer.

`KeyboardStudio.Windows` stays referenced, registered, and tested; `BuildTarget.WindowsX64` stays in
the enum; and `windowsX64` stays a persisted profile discriminator so existing documents round-trip
their Windows profile unedited. `BuildOrchestrator` and `IBuildBackendResolver` are unchanged, so
AD-016 single-target dispatch still resolves whichever target it is given — the UI simply never asks
for the hidden one. `KEYBOARDSTUDIO_TARGETS=all` restores the full selector for development and for
tests. Visibility is never expressed by deleting profiles, mutating `BuildOptions`, or skipping
validation, so the Windows path cannot rot while it is hidden.
