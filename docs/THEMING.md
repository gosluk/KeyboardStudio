# KeyboardStudio Theming and Application Shell Architecture

## 1. Status and scope

This document defines the architecture for Phase 15: explicit White, Gray, and Black application
themes; local persistence of the selected theme; a simplified application header; and a startup path
that opens directly onto an editable layout without requiring the user to press **Create**.

This is the architecture baseline, and it is implemented: every acceptance criterion in section 13
is met and covered by the tests named in the
[MVP release checklist](MVP-RELEASE-CHECKLIST.md). The executable work order is in
[`THEMING-IMPLEMENTATION-PLAN.md`](THEMING-IMPLEMENTATION-PLAN.md).

Two decisions were taken during implementation and are recorded in
[`DECISIONS.md`](DECISIONS.md): Fluent's palette accent is pinned per inherited variant, because it
otherwise follows the desktop's accent colour into every check mark and list selection (AD-038); and
diagnostics collapse when there is nothing to report, expanding on the transition into error rather
than on every validation pass (AD-039).

The supplied wireframes define visual character, not control placement. They establish three tonal
directions and a compact editor hierarchy; they do not require custom window chrome, a permanent
navigation rail, or controls that the current product does not need.

The phrase "current layout" means the host's active keyboard layout when a supported import source
can resolve it. On a host where that layout cannot be imported, KeyboardStudio opens the populated
`us-basic` seed immediately. Reopening the last `.kbdproj` is not part of this phase.

## 2. Goals

Phase 15 must:

- provide explicit White, Gray, and Black themes;
- apply a theme to the whole application, including dialogs, menus, tooltips, and keyboard keys;
- remember the selected theme in a per-user local JSON file;
- apply the saved theme before the first window is constructed;
- keep user appearance settings out of `.kbdproj` documents;
- open a fresh window onto an editable host layout, with the populated seed as a safe fallback;
- remove **New from / Create** from the primary editor toolbar;
- move the File menu beside the `KeyboardStudio` title and render its trigger as an accessible icon;
- preserve existing file commands, shortcuts, dirty-state protection, and startup race protection;
- improve hierarchy, readability, and progressive disclosure without changing the domain model.

## 3. Non-goals

Phase 15 does not include:

- a user-authored theme editor;
- arbitrary accent-color selection;
- downloading or installing theme packages;
- storing appearance in project documents;
- synchronizing preferences between machines;
- reopening the most recently used project;
- custom title bars or replacement window controls;
- a Windows installed-layout importer;
- changing keyboard geometry, layout-import semantics, build orchestration, or artifact formats;
- a permanent navigation rail copied from the wireframes.

## 4. Architectural boundaries

Themes are application presentation state. They do not describe a keyboard layout and therefore do
not belong in `KeyboardStudio.Core` or the `.kbdproj` schema.

```text
settings.json
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

IStartupLayoutLoader ----> StartupLayoutResult
                                  |
                                  v
MainWindowViewModel ----> replace only an untouched startup document
```

The application project owns all of these types because they coordinate Avalonia presentation,
application startup, and host-local preferences. `KeyboardStudio.Persistence` remains responsible
for portable keyboard project documents and must not acquire a dependency on Avalonia theme names.

Every new top-level C# type is placed in its own file in accordance with `AGENTS.md`.

## 5. Local application settings

### 5.1 Contract

The first settings schema contains only the theme but is versioned so later preferences do not
require replacing the storage boundary.

```json
{
  "schemaVersion": 1,
  "theme": "gray"
}
```

The persisted theme identifiers are the stable lower-case strings `white`, `gray`, and `black`.
They are not CLR enum names and are not Avalonia `ThemeVariant` keys on the wire.

Proposed application types:

```text
ApplicationSettings
ApplicationTheme
IApplicationSettingsStore
JsonApplicationSettingsStore
IApplicationSettingsPathProvider
LocalApplicationSettingsPathProvider
```

`ApplicationSettings` is an application preference snapshot. `ApplicationTheme` is the neutral
choice used by ViewModels and persistence. Only the theme service translates it to Avalonia.

### 5.2 File location

`LocalApplicationSettingsPathProvider` resolves:

```text
<Environment.SpecialFolder.LocalApplicationData>/KeyboardStudio/settings.json
```

Typical locations are:

- Windows: `%LOCALAPPDATA%\KeyboardStudio\settings.json`;
- Linux: `${XDG_DATA_HOME:-$HOME/.local/share}/KeyboardStudio/settings.json`;
- macOS, if supported later: `~/Library/Application Support/KeyboardStudio/settings.json`.

The path provider is injected into the store so tests always use an isolated temporary directory and
never read or write the developer's real preferences.

### 5.3 Read and write policy

Loading settings is best effort. A missing file, invalid JSON, unknown theme, unsupported schema,
access failure, or I/O failure returns the default settings and never prevents the editor from
opening. The original unreadable or future-version file is left untouched.

Gray is the deterministic default. It is the closest visual continuation of the existing neutral
workspace and gives tests and screenshots a stable initial result independent of the operating
system theme.

An explicit theme selection is saved immediately. Theme changes are infrequent, so a debounce adds
state and failure modes without meaningful benefit. Saving uses a same-directory temporary file and
an atomic replacement or move so an interrupted write cannot leave a partially written JSON file.
Temporary files are removed on a best-effort basis after a failure.

A settings failure is traced for diagnostics but does not show a modal startup dialog. If saving a
user-initiated change fails, the theme remains applied for the current session and the appearance UI
shows a concise non-modal warning.

Avalonia's application-settings guidance recommends JSON under the per-user local application-data
directory and a default-on-corruption policy:
<https://docs.avaloniaui.net/docs/how-to/data-persistence-how-to>.

## 6. Theme runtime

### 6.1 Theme service

Proposed types:

```text
IApplicationThemeService
AvaloniaApplicationThemeService
ApplicationThemeVariants
AppearanceViewModel
ThemeOptionViewModel
```

`IApplicationThemeService` exposes the current neutral `ApplicationTheme` and applies a new choice.
`AvaloniaApplicationThemeService` is the only type that accesses
`Application.RequestedThemeVariant`. This keeps the ViewModel testable without constructing an
Avalonia application lifetime.

`AppearanceViewModel` owns the selectable theme options, selected state, command, save status, and
immediate persistence. It does not manipulate resource dictionaries directly.

### 6.2 Custom variants and fallback

KeyboardStudio defines three custom variants:

```text
White  -> inherits ThemeVariant.Light
Gray   -> inherits ThemeVariant.Light
Black  -> inherits ThemeVariant.Dark
```

The fallback supplies complete Fluent control resources for properties KeyboardStudio does not
override. The custom variant supplies KeyboardStudio's semantic tokens. White and Gray use light
control semantics; Black uses dark control semantics, including supported native window decoration.

Avalonia supports custom variants with an inherited Light or Dark fallback:
<https://docs.avaloniaui.net/docs/how-to/theme-switching-how-to>.

### 6.3 First-frame behavior

The composition root performs startup in this order:

1. load `App.axaml` and its theme/resource definitions;
2. construct the settings store and load preferences;
3. apply the chosen custom theme to the `Application`;
4. construct `MainWindow` and its ViewModels;
5. assign the window to the desktop lifetime;
6. start current-layout loading asynchronously.

Applying the theme before step 4 prevents a flash of the default Fluent or operating-system theme.

## 7. Semantic resource system

### 7.1 File organization

The intended style structure is:

```text
Styles/
 |- ThemeResources.axaml   White, Gray, and Black theme dictionaries
 |- AppStyles.axaml        structural and state styles using semantic resources
 `- IconResources.axaml    application-owned vector icon geometries
```

`KeyControl.axaml` may keep its control-specific selectors and template, but every color, border,
and shadow it consumes comes from the application token contract.

All theme-dependent references use `DynamicResource`. Avalonia only resolves resources from theme
dictionaries dynamically; `StaticResource` is not valid for resources that change with the active
variant:
<https://docs.avaloniaui.net/docs/styling/theme-variants>.

### 7.2 Token contract

Each theme defines exactly the same keys. The contract covers:

- `AppSurfaceBrush`, `WorkspaceSurfaceBrush`, `PanelSurfaceBrush`, and `ElevatedSurfaceBrush`;
- normal, subtle, strong, selected, and focus border brushes;
- primary, secondary, disabled, inverse, and link foreground brushes;
- accent, accent-hover, accent-pressed, and accent-foreground brushes;
- success, warning, danger, and informational surface/foreground/border brushes;
- menu, tooltip, badge, input, button, and selection resources;
- keyboard bezel, key face, hover, pressed, selected, active-layer, error, legend, and hint resources;
- theme-aware card and key shadows.

No view or control may introduce a literal presentation color. Constants that are not
theme-dependent, such as geometry and spacing, remain structural styles rather than theme tokens.

### 7.3 Visual direction

The initial anchors below describe relationships, not final approved color values. Implementation
may adjust them to satisfy contrast and avoid visual vibration on real displays.

| Theme | Direction | Initial anchors |
| --- | --- | --- |
| White | Bright, airy workspace with lightly elevated white cards and keys | `#F6F8FC` workspace, white panels, dark navy text, blue accent |
| Gray | Cool neutral shell with raised gray keycaps and darker structural borders | `#B9C0C8` workspace, `#CCD1D6` panels, near-black text, blue accent |
| Black | Near-black shell with charcoal panels and keycaps, restrained blue selection | `#0F1318` workspace, `#1A1F25` panels, `#353B43` keys, near-white text |

The same blue accent family identifies focus, selection, links, and primary actions in all three
themes. Warning, error, and success colors are tuned per background rather than reused blindly.

Normal text targets WCAG AA contrast of at least 4.5:1. Large text, focus indicators, control
boundaries, and meaningful graphical states target at least 3:1 against adjacent colors. Selected,
warning, and error states also use borders, icons, or text so color is never their only signal.

## 8. Application header and file menu

The current standalone menu row is replaced by one application header:

```text
KeyboardStudio  [file icon]  •  layout or filename  [document state]    [appearance icon]
```

The file icon opens the existing document menu. It is a local vector `PathIcon` or equivalent
`StreamGeometry`; Phase 15 does not add an icon package or raster asset. The trigger retains the
accessible name `File`, a tooltip such as `File menu`, keyboard focus, and the existing access and
command paths.

Existing shortcuts remain unchanged:

- New: `Ctrl+N`;
- Open: `Ctrl+O`;
- Save: `Ctrl+S`;
- Save As: `Ctrl+Shift+S`.

The appearance icon opens three mutually exclusive, keyboard-accessible theme choices. Each choice
shows both its name and selected state; the icon is not the only accessible label.

The header shows a concise project/layout name. A full project path appears in a tooltip instead of
occupying the complete header. Dirty, loading, imported, and fallback states remain visible but use
compact semantic badges and status text.

## 9. Fresh-window layout behavior

### 9.1 Loader boundary

Proposed types:

```text
IStartupLayoutLoader
StartupLayoutLoader
StartupLayoutResult
StartupLayoutStatus
```

`StartupLayoutLoader` depends on `ILayoutImportCatalog` and `IHostLayoutProbe`. It performs detection
and import and returns a result; it does not mutate a document, ViewModel, or Avalonia control.

`MainWindowViewModel` remains the document owner. It decides whether a successful result can replace
the startup document and turns the result into document status or diagnostics.

### 9.2 Startup sequence

1. Construct and render the populated seed project immediately.
2. Set non-blocking status to `Loading current layout...`.
3. Run the loader away from the UI thread.
4. On success, adopt the imported document only if the startup document is the same instance, clean,
   and pathless.
5. On unavailable or failed import, keep the seed editable and show a concise informational status
   with the existing Import path available.
6. If the user edits, opens, imports, or creates a document before loading finishes, discard the
   startup result.

The sequence preserves the existing guarantee that slow host data cannot delay the first frame and
that background completion cannot overwrite user work.

### 9.3 Removing Create from the normal path

The permanent `New from [template] [Create]` group is removed from the keyboard toolbar. A fresh
window already contains the layout the user is expected to edit.

New-project creation becomes secondary document navigation:

- `Ctrl+N` creates a populated seed using the current document's geometry;
- the File icon menu offers explicit ISO-105 and ANSI-104 new-document choices;
- all paths continue through the current unsaved-changes confirmation;
- Import remains available in the File menu.

Changing the geometry selector must never silently replace the open project.

The current host-layout source is Linux-specific. Windows continues to receive the populated seed
until a Windows import source implements the existing neutral import contracts.

## 10. UX review and Phase 15 scope

### 10.1 Committed refinements

Phase 15 includes these refinements because they support the new visual system and remove current
hierarchy problems:

- collapse Diagnostics to a summary row when it has no actionable entries;
- auto-expand Diagnostics when an error appears while leaving manual control to the user;
- define primary, secondary, quiet, and destructive button styles;
- use the primary style for Build and explicit commit actions, not for every button;
- raise very small supporting text and interactive targets where the existing density harms
  readability or keyboard/mouse use;
- use semantic resources for dirty, import, warning, and build-problem states;
- keep the keyboard as the dominant surface and make secondary cards visually quieter;
- use a concise filename/layout label plus full-path tooltip in the header.

### 10.2 Follow-up candidates

These ideas are worthwhile but should be planned separately after Phase 15 is evaluated:

- tabs or a dedicated inspector model for Selected key, Build, and Linux user-variant workflows;
- keyboard zoom and a `Fit keyboard` command;
- persistence of window size, panel expansion, and other workspace state;
- a recent-projects menu;
- automated screenshot baselines;
- a Windows installed-layout import source.

## 11. Failure behavior

| Failure | Required behavior |
| --- | --- |
| Settings file missing | Start in Gray; do not create the file until the user changes a preference |
| Settings JSON invalid or future schema | Preserve the file, start in Gray, trace the failure |
| Theme save denied or interrupted | Keep the selected theme for the session; show a non-modal warning |
| Theme resource key missing | Fail automated resource-contract validation before release |
| Host layout unavailable | Keep the populated seed editable; show informational fallback status |
| Host import fails | Keep the seed; retain the existing non-modal diagnostic behavior |
| User acts before startup import finishes | Preserve the user's document and discard the late result |
| Unsupported platform has no importer | Use the populated seed without presenting an error dialog |

## 12. Test architecture

`KeyboardStudio.App.Tests` owns the new automated coverage:

- settings path resolution uses injected roots and platform-independent assertions;
- missing, valid, invalid, future, unknown-theme, access, and interrupted-write cases;
- stable lower-case theme serialization and round trip;
- application-theme service mapping to White, Gray, and Black variants through a fake host boundary;
- selection applies immediately and saves exactly once;
- settings load occurs before window construction in the composition sequence;
- every theme dictionary defines exactly the required semantic token set;
- no application view contains an unapproved literal presentation color;
- startup loader success, unavailable, failure, and cancellation;
- successful startup replacement, late-result rejection, and seed fallback;
- file commands and keyboard shortcuts remain wired after the header move.

Release verification also includes a manual visual matrix:

- White, Gray, and Black;
- main window plus every modal dialog, menu, tooltip, and popup;
- minimum and normal window sizes;
- 100%, 150%, and 200% display scaling;
- Linux and Windows packages;
- normal, hover, pressed, focus, disabled, selected, warning, error, and success states;
- keyboard-only navigation and accessible names for icon-only controls.

## 13. Acceptance criteria

The architecture is realized when:

1. a theme change updates every open application surface immediately;
2. closing and reopening restores the selected theme without a first-frame flash;
3. the preference is present only in the local settings file and never changes `.kbdproj` output;
4. missing or damaged settings cannot prevent startup;
5. White, Gray, and Black implement one complete semantic resource contract;
6. a fresh supported Linux session edits the detected current layout without pressing **Create**;
7. a failed or unsupported host import leaves a populated, editable seed;
8. startup import never overwrites a document the user has touched or replaced;
9. the File icon sits beside `KeyboardStudio` and exposes all current file commands accessibly;
10. the permanent **New from / Create** toolbar group is gone;
11. all existing document lifecycle, import, build, and packaging tests remain green;
12. all touched C# files contain no more than one top-level type.

