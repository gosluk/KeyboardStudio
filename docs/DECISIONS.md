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

The `KSI` diagnostic codes are declared in Core too, unlike the `KSL` codes that belong to
`KeyboardStudio.Linux`. What import loses is a property of the domain model rather than of a file
format, so a second source would report the same losses, and one shared range stops two sources from
giving one number two meanings. `LayoutImportCatalog` skips a source that reports itself unavailable
but lets a failure from an available one propagate: returning a silently shorter list would leave the
user hunting for an installed layout with nothing on screen to explain its absence.

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

The fidelity level is derived by `LayoutImportReport.Classify` rather than by each source: any
skipped key makes an import `Partial`, any finding above `Info` makes it `Reduced`, and everything
else is `Exact`. Left to the sources, `Reduced` would come to mean something different per platform
and the badge in the import dialog would stop carrying information.

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

The policy filters the target list only. Profiles are still constructed for every target, so
`ExportTargetProfiles` keeps returning both entries whatever is on screen, and a policy that hid
everything falls back to the full list rather than producing a Build card with nothing to build.

## AD-025 - Import previews by importing, and commits in two ways

Selecting a layout in the import dialog runs the real import. The result is the preview, the
fidelity report, and — if accepted — the committed project.

Import is lossy by design, so a report the user cannot see until after they commit is a report they
cannot act on. Importing once and reusing the result also removes the class of bug where the preview
and the commit disagree, which a second import to commit would reintroduce. The preview keycaps are
the editor's own `KeyViewModel` with no select command attached, for the same reason: a second
rendering can disagree with the first, and the empty-keyboard defect fixed in P13.9 was invisible
until a project was drawn.

An accepted import commits one of two ways. **A new project** replaces the document outright and
carries default build settings with the XKB profile pre-filled; it has no path and is not dirty,
exactly like a new document. **Replacement mappings** keep the open document's geometry, build
settings and file, changing only what the keys produce, and mark it dirty. The dialog pins the
geometry to the open document in that mode, because that document's keyboard is what the mappings
must land on. Both paths run the existing unsaved-changes prompt, after the dialog rather than
before it: both discard work in progress, and neither is worth prompting about until the user has
said which one they want.

`LayoutImportViewModel` depends on `ILayoutImportCatalog` and on geometry descriptors, never on a
source. The commit decision lives in `MainWindowViewModel`, where the document lives; the dialog
imports and reports, and decides nothing.

## AD-026 - A file the user names is a second source, not a mode of the first

`XkbSymbolsFileImportSource` (`linux-xkb-file`) imports one symbols file by path.
`XkbLayoutImportSource` (`linux-xkb`) imports what the host advertises. They are separate sources.

The two answer different questions — "what can I import?" against "import this" — and provenance has
to record which of them a document came from: a catalogued layout can be found again by name, a
loose file only by path. The file source therefore lists nothing, and a file outside the roots is
never offered for browsing, because that would mean guessing where the user keeps their layouts.

The picked file answers to its own name through `XkbPinnedFileIncludeResolver`, so a section it
includes from itself reaches that file rather than an installed layout that happens to share the
name — the same shadowing an XKB root of one's own performs. Its other includes still resolve out of
the installed database, which is the only place `latin` and `us` exist. That is also why the source
reports itself unavailable without a database: symbols files are written as differences, so importing
one without the database yields the dozen keys it overrides and a report full of missing includes.

## AD-027 - The host's layout is detected from files, and only replaces a document nobody has touched

Startup detection reads `XKB_DEFAULT_LAYOUT`, then `/etc/X11/xorg.conf.d/00-keyboard.conf`, then
`/etc/vconsole.conf`, then `/etc/default/keyboard`, then falls back to `us`. No process is spawned.

`localectl` and `gsettings` would both answer the question, but each adds a process dependency to
the startup path, each depends on which desktop happens to be installed, and both write the files
above anyway. Files are readable whether or not anything is running, which matters on a machine
where the editor may be among the first things started.

The chain has two wrinkles worth stating. `KEYMAP` in `vconsole.conf` names a console keymap rather
than an XKB layout — a host set to `KEYMAP=pl2` has no XKB layout called `pl2` — so `XKBLAYOUT` in
the same file outranks it, while `KEYMAP` stays as the only statement of intent a console-only host
makes. And `/etc/default/keyboard` is in the chain because it is where the Debian family keeps
`XKBLAYOUT`; without it the whole feature falls through to `us` on the distributions most likely to
have a layout worth detecting.

Detection is total rather than nullable: it ends at `us`, so the caller never has to decide what
nothing means, and whether that layout exists is the import's question to answer. Detection speaks
XKB's vocabulary because that is what it reads; Core sees `IHostLayoutProbe` returning an
`ImportableLayoutReference`, and `XkbHostLayoutProbe` is the one line between them.

The import replaces the open document only while it is still the untouched one the editor started
with — same instance, not dirty, no path. Nobody asked for this import, so a user who has typed,
opened a file, or made a new document has already said what they want to work on, and having it
swapped out a moment later would be worse than never importing at all. For the same reason failure
is quiet: a host with no source reports nothing, and a layout that could not be read leaves a
`KSI011` `Info` entry rather than a dialog about a layout the user never mentioned.

## AD-028 - Pinned fixtures say what an import produces, the host says whether it copes

Two kinds of test read XKB data, and they need different inputs. The corpus soak imports everything
the host advertises, which is the only way to meet the variety real layouts have; it cannot assert
what any particular import produces, because the answer changes whenever the distribution updates
xkeyboard-config. The goldens assert exactly that, so they read a copy of `us`, `pl`, `de` and `fr`
vendored into the test fixtures and pinned to a recorded version.

The vendored files are whole upstream files, not excerpts. A trimmed symbols file is no longer the
thing upstream ships, which is the one property that makes vendoring it worth doing at all — and
hand-trimming risks a fixture that quietly disagrees with the real one. The cost is 436 KB of test
input and a script, `scripts/vendor-xkb-fixtures.py`, that copies the include closure and the
registry entries again when the pin moves.

`xkbcli` is used as a conformance oracle rather than only as a compiler. Every other test grades the
importer against something this project wrote; only libxkbcommon can say whether flattening
`pl(basic)` — `latin`, then four overrides — leaves the layout the system would produce. The
comparison is by physical key rather than by key name, because `keycodes/evdev` gives most keys two
names and a phonetic layout writes both, and by decoded output rather than by keysym name, because
`U0105` and `aogonek` are one answer written two ways. It reads the host's database rather than the
pinned fixtures so that both sides see the same bytes, which keeps a version difference from
presenting as a defect.

Goldens are rewritable by design: `KEYBOARDSTUDIO_UPDATE_GOLDEN=1` rewrites every snapshot in the
repository. A golden nobody can regenerate gets asserted around instead of updated, and a diff of
what an import now produces is exactly the artifact a change to the importer should be reviewed
through.

## AD-029 - Linux customization is a variant of an imported system layout

The Phase 14 product workflow begins with a system-origin XKB import and publishes supported edits as
a new variant beneath the original layout identity. It does not create a new language or copy the
whole system definition.

KeyboardStudio is a partial augmentation editor. Keeping `pl` or `al` as the RMLVO layout lets the
desktop continue to associate the result with Polish or Albanian while a custom variant ID and
description distinguish the user's work. The generated section includes the current system base and
overrides changed keys only, so unmodified keys continue to receive distribution updates.

Loose-file imports, authored projects, and version-2 imported projects without a baseline remain
valid for standalone export but are not eligible for derived-variant installation in the first
implementation.

## AD-030 - Definitions are dedicated; language association uses thin bridges and registry overlays

All substantial KeyboardStudio definitions live in the user file `symbols/keyboardstudio`. For each
base layout, a small managed section in `symbols/<base-layout>` forwards the public variant to the
dedicated internal section. A minimal user `rules/evdev.xml` entry advertises that public variant
beneath the existing system layout.

The normal rules catch-all resolves `(pl, keyboardstudio_example)` as
`pl(keyboardstudio_example)`, which is why a same-name bridge is required. The bridge must not copy
or replace the distribution's other sections. The dedicated definition includes its base as
`%S/pl(qwertz)`, explicitly selecting the system root and avoiding recursion through the user file.

Shared bridge and XML files are edited surgically. KeyboardStudio preserves unrelated user content,
refuses ownership conflicts, and removes only nodes or managed blocks carrying its installation ID.

## AD-031 - Per-user XKB installation is explicit, transactional, and capability-gated

Normal builds never modify an XKB configuration root. Install, update, verify installed, and
uninstall are separate explicit operations targeting
`${XDG_CONFIG_HOME:-$HOME/.config}/xkb`; activation remains the desktop's responsibility.

The managed install command is offered only when the host demonstrates the required libxkbcommon
Wayland path, `%S` support, writable safe XDG locations, and `xkbcli` compilation verification. X11,
old or unknown libxkbcommon, and unverifiable hosts receive export-only behavior. Registry discovery
is reported separately because a desktop that parses XML directly may not merge the user registry
the way libxkbregistry does.

The installer first constructs and compiles the exact merged root in a staging directory. It also
compiles the base and an unrelated same-layout variant, then writes with backups, same-directory
temporary files, a journal, hashes, and rollback. It never writes `/usr/share`, `/etc/xkb`, or a
desktop/session setting.

## AD-032 - A derivation baseline is portable; installation state is host-local

Document schema version 3 adds an immutable `LayoutDerivation` beside import provenance. It records
the system base identity and the representable mappings at import time. A platform-neutral Core diff
compares that snapshot with the current project; if one supported layer changes, Linux generation
emits the complete current supported mapping for the key.

Provenance alone cannot calculate this delta, and re-reading the current system layout would confuse
distribution changes with user edits. If a changed key contained source behavior the importer could
not represent, generation fails rather than silently erasing that behavior.

Paths, installed variants, content hashes, backups, and transaction journals describe one machine,
so they live under `${XDG_STATE_HOME:-$HOME/.local/state}/keyboardstudio/xkb`, not in `.kbdproj`.
This keeps project files portable and makes ownership checks independent of project renames or file
copies.

## AD-033 - Appearance settings are local application state, not project data

The selected White, Gray, or Black application theme is stored in a versioned JSON file beneath the
current user's local application-data directory. It is never serialized into `.kbdproj` and never
marks the open keyboard project dirty.

Appearance describes how one installation presents every document, not what a keyboard project
means. Putting it in the project envelope would make opening a file unexpectedly restyle the whole
application, create meaningless project diffs, and couple portable persistence to Avalonia. The
settings contract and store therefore live in `KeyboardStudio.App`; `KeyboardStudio.Persistence`
remains responsible for portable keyboard documents.

Missing, damaged, inaccessible, unknown, or future settings fall back to Gray without blocking
startup. Explicit changes are saved immediately through an atomic same-directory replacement.

## AD-034 - Three custom variants share one semantic resource contract

KeyboardStudio defines White and Gray variants that inherit Avalonia Light and a Black variant that
inherits Avalonia Dark. Fluent supplies complete fallback control semantics; KeyboardStudio supplies
the application surfaces, keycaps, state colors, borders, text, and shadows through semantic dynamic
resources.

All three theme dictionaries define the same required keys. Views and control templates consume
those keys through `DynamicResource` and do not contain literal presentation colors. This makes a
missing theme value detectable as a contract failure and lets the application switch variants
without rebuilding the visual tree.

Gray is the deterministic first-run default. Following the operating-system variant remains outside
the first three-theme scope.

## AD-035 - The saved theme is applied before the first window is constructed

The application composition root loads local settings and applies the selected custom theme to
`Application.RequestedThemeVariant` before constructing `MainWindow`.

Applying the preference from a window ViewModel would render the first frame with the wrong Fluent
variant and then correct it visibly. Theme selection remains testable through an application-theme
service boundary; only its Avalonia implementation touches `Application`.

A user selection applies immediately and then saves. A save failure leaves the requested theme
active for the current session and reports a non-modal warning rather than rolling the UI back.

## AD-036 - Startup layout loading returns data; the document owner decides replacement

Host detection and import move behind `IStartupLayoutLoader`, which returns a structured result but
does not mutate a project, ViewModel, or Avalonia control. `MainWindowViewModel` remains the document
owner and adopts a result only while the original startup project is the same instance, clean, and
pathless.

The populated seed still renders immediately, so host file I/O cannot delay the first frame. A
supported Linux host then settles on its active layout without user action. Unsupported or failed
imports leave the seed editable and present non-modal fallback information. A late result can never
overwrite edits, Open, Import, or New.

The permanent `New from [template] [Create]` toolbar group is removed. New-project creation remains
available through the File menu and `Ctrl+N`, behind the existing unsaved-changes confirmation.

## AD-037 - Document commands use an accessible icon menu in the application header

The standalone File menu row moves into the application header immediately beside the
`KeyboardStudio` title. Its trigger is an application-owned vector icon with the accessible name
`File`, tooltip text, visible keyboard focus, and all existing commands and shortcuts.

An adjacent Appearance trigger exposes White, Gray, and Black as mutually exclusive named choices.
Icon-only presentation does not remove textual automation names or keyboard access. KeyboardStudio
uses local vector resources rather than adding an icon package for two application-shell symbols.

The header shows a concise layout or filename and puts the full path in a tooltip. This keeps dirty,
loading, import, and fallback state visible without allowing a long path to consume the header.

## AD-038 - Fluent's palette accent is pinned per inherited variant

`FluentTheme.Palettes` sets the accent Fluent uses for its own check marks, radio dots, list
selection, and scrollbars. Left alone it follows the desktop's accent colour, so a KeyboardStudio
theme would show a checkbox in whatever colour the host was configured for — a colour belonging to
none of the three palettes, and one that changes under the application without warning.

`Palettes` accepts the Light and Dark variants only. Each KeyboardStudio variant is therefore served
by the one it inherits: White and Gray from Light, Black from Dark. This is the single exception to
the rule that only `ThemeResources.axaml` names a colour, and the literal-colour audit permits it
inside `ColorPaletteResources` and nowhere else.

Overriding individual Fluent resource keys was rejected. Those keys are implementation detail and an
Avalonia upgrade could rename them without failing a build; the palette accent is the documented
customization point and is one value rather than a list.

## AD-039 - Diagnostics collapse when there is nothing to report

The diagnostics panel occupies one line while it has no entries, and expands on the transition into
error rather than on every validation pass.

A clean document is the normal case, so a panel that permanently reserved the bottom of the editor
to say "No diagnostics" was taking that space from the keyboard to display nothing. Expanding on
every refresh would fight the user instead: validation reruns on each edit, so a list they had just
closed would reopen at the next keystroke for as long as the error stood. The panel therefore opens
itself only when an error first appears, and the user's own collapse holds until the next one.

The summary names severities — "1 error, 2 warnings" — rather than counting rows, so it reads the
same with colour removed.

## AD-040 - An imported layout is composed onto the XKB common base

`IXkbSymbolsResolver.ResolveLayout` merges `symbols/pc` before the layout, the way `rules/evdev`
does with its `pc+%l%(v)` fallback for every model and layout.

A symbols file is not a keyboard. `pl` writes the two dozen keys that make a layout Polish, and
Escape, the function row, the modifiers, the editing block, the arrows and the keypad are all
somewhere else. Importing the file alone produced a board with 50 of 105 keys carrying any output —
read on screen as a keyboard whose function row and keypad had no logical keys assigned, which is
not a partial import of Polish but a misreading of how layouts are written. Composed, the same
layout imports 105 keys.

Every resolved key records whether the base or the layout defined it, because the base is identical
for every layout and so says nothing about any one of them. Geometry inference ignores a `<LSGT>`
that only the base wrote, or it would suggest ISO for every layout there is; and a loss inside a
base key — a fifth level the model cannot hold, a key no template has — is neither counted against
the import nor reported, because saying the same thing about every layout in the database buries
the findings that describe the one being imported.

The alternative, filling unmapped keys from the seed project after import, was rejected: it would
invent mappings the source never contained and make an import's output depend on which seed shipped.

## AD-041 - The import catalog offers layouts, ordered by the name it shows

A symbols file the registry does not describe is listed only when it names a keyboard group, and the
list is ordered by display name rather than by layout identifier.

Two thirds of a distribution's `symbols/` directory is components — `pc`, `latin`, `level3`,
`capslock`, `keypad`, `altwin` — merged into a layout rather than chosen as one. Listing every file
put `altwin` between Albanian and Armenian in what reads as a list of countries, and offered entries
that import three keys. `name[Group1]` separates the two in the data: a layout names the group it
defines, and a component cannot, because it would then be naming every layout it is merged into. The
test reads the file's own default section and follows no includes, so `latin` does not inherit
"English (US)" from the `us` section it composes. A layout the user wrote themselves still has to
name its group for the desktop to offer it, so it is still listed here.

Filtering to registry entries alone was rejected: it is what the desktop settings panels do, but it
would drop the user's own layouts, which are the ones this application exists to edit.

Ordering by identifier showed Dari above Albanian and Chinese below English — the column the user
scans is the name, and the code that explains the order is not in it.
