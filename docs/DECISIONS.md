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

## AD-009 - General and Windows metadata are separate

`ProjectMetadata` contains only cross-platform information: display name, description, user-managed project version, and language/locale. Windows layout identity is represented by `WindowsLayoutMetadata` in `KeyboardStudio.Windows` and must not be added to `KeyboardStudio.Core`.

Persistence DTOs must not solve target metadata by making `KeyboardStudio.Persistence` depend on the Windows backend or by putting Windows fields into the core aggregate. The current `IKeyboardProjectStore` transports only the platform-neutral `KeyboardProject`; target-specific document/settings persistence must be introduced through a boundary that can preserve Windows metadata without reversing dependency direction.

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
