# KeyboardStudio Theming and Application Shell Implementation Plan

## 1. Purpose

This plan implements the architecture in [`THEMING.md`](THEMING.md). It is Phase 15 of the main
[`IMPLEMENTATION-PLAN.md`](IMPLEMENTATION-PLAN.md).

The order is intentional: architectural review comes first, infrastructure precedes visual changes,
and startup behavior is separated from the appearance work so each boundary can be tested before the
main window is rearranged.

No production implementation begins until P15.1 is complete.

## 2. Work-item summary

| Work item | Outcome | Depends on |
| --- | --- | --- |
| P15.1 | Approve architecture, token contract, and UX acceptance criteria | none |
| P15.2 | Add resilient local application-settings persistence | P15.1 |
| P15.3 | Add the application theme runtime and first-frame restoration | P15.2 |
| P15.4 | Implement White, Gray, and Black semantic resources | P15.3 |
| P15.5 | Add theme selection and rebuild the application header/file menu | P15.4 |
| P15.6 | Extract current-layout startup and remove Create from the normal path | P15.1 |
| P15.7 | Apply the committed UX hierarchy and accessibility refinements | P15.5, P15.6 |
| P15.8 | Close automated, cross-platform, and visual verification | P15.2-P15.7 |

## 3. P15.1 — Architecture and acceptance gate

### Goal

Establish the decisions that later work must implement without mixing design decisions into code
review.

### Work

- Review [`THEMING.md`](THEMING.md).
- Confirm Gray as the deterministic first-run default.
- Confirm local settings are application state, not project persistence.
- Confirm the three custom-variant fallback relationships.
- Confirm the semantic resource-key contract and the ban on view-local presentation colors.
- Confirm that "current layout" means the active host layout where supported, with populated seed
  fallback elsewhere.
- Confirm the File icon, Appearance icon, and secondary new-document workflow.
- Confirm committed versus follow-up UX improvements.
- Record accepted decisions in [`DECISIONS.md`](DECISIONS.md) and link the architecture from
  [`ARCHITECTURE.md`](ARCHITECTURE.md).

### Exit criteria

- Architecture, ownership, failure behavior, and non-goals are unambiguous.
- No unresolved choice can change a public/internal contract introduced by P15.2-P15.6.
- No C# or XAML production file has been changed as part of the gate.

## 4. P15.2 — Local application settings

### Goal

Persist the selected application theme independently from keyboard projects.

### Production changes

Add one top-level type per file under an application-owned settings subsystem:

```text
ApplicationSettings.cs
ApplicationTheme.cs
IApplicationSettingsStore.cs
JsonApplicationSettingsStore.cs
IApplicationSettingsPathProvider.cs
LocalApplicationSettingsPathProvider.cs
```

Implement:

- schema version 1 with stable `white`, `gray`, and `black` values;
- Gray defaults;
- `LocalApplicationData/KeyboardStudio/settings.json` resolution;
- injected path resolution for tests;
- asynchronous load/save APIs with cancellation where file I/O can wait;
- tolerant missing/corrupt/unknown/future settings reads;
- same-directory temporary writes and atomic replacement;
- best-effort temporary cleanup;
- presentation-safe error results rather than startup exceptions.

Do not add these types to `KeyboardStudio.Persistence`: that assembly persists portable project data,
whereas this file contains host-local application preferences.

### Tests

- missing file returns Gray and does not create a file;
- valid White, Gray, and Black files load correctly;
- save/load round trips stable lower-case identifiers;
- invalid JSON, unknown theme, and future schema fall back without modifying the source file;
- a blocked or invalid path returns a failure result without throwing through the application;
- a successful save leaves no temporary file;
- a failed replacement does not destroy the last complete settings file;
- all tests use isolated temporary paths.

### Exit criteria

- Settings behavior is deterministic on Linux and Windows.
- No test reads or writes the real user profile.
- `.kbdproj` serialization is byte-for-byte unaffected for identical project input.

## 5. P15.3 — Theme runtime and first-frame restoration

### Goal

Translate the neutral saved preference into an application-wide Avalonia theme before the first
window exists.

### Production changes

Add:

```text
IApplicationThemeService.cs
AvaloniaApplicationThemeService.cs
ApplicationThemeVariants.cs
```

Implement custom variants:

```text
White -> Light fallback
Gray  -> Light fallback
Black -> Dark fallback
```

Update the application composition root to:

1. load settings;
2. apply the requested theme;
3. create `MainWindow` and its ViewModels;
4. continue startup layout import asynchronously.

Remove `RequestedThemeVariant="Default"` as the product policy. The application must not follow the
operating-system theme while the saved choice is White, Gray, or Black.

### Tests

- each neutral theme maps to the correct custom Avalonia variant;
- applying the same theme is idempotent;
- a missing/invalid preference applies Gray;
- the composition sequence applies the theme before window construction;
- changing variants does not rebuild or replace the current project/ViewModel.

### Exit criteria

- The saved preference controls `Application.RequestedThemeVariant`.
- The main window never renders once in an unrelated theme before correction.
- Theme infrastructure has no dependency on keyboard domain or project persistence types.

## 6. P15.4 — White, Gray, and Black resources

### Goal

Turn the wireframe directions into one complete, semantic, application-wide resource contract.

### Production changes

- Add `Styles/ThemeResources.axaml` with dictionaries keyed to the three custom variants.
- Move icon geometries to `Styles/IconResources.axaml`.
- Keep layout and control structure in `Styles/AppStyles.axaml`.
- Replace all view-local presentation colors and fixed shadows with `DynamicResource` references.
- Extend tokens to menus, tooltips, inputs, selection, diagnostics, cards, keyboard bezel, keycaps,
  build problems, and dirty/import states.
- Add explicit primary, secondary, quiet, and destructive button classes.
- Preserve `KeyControl` as the owner of its template while making its appearance token-driven.

Do not override undocumented Fluent internal resource keys merely to force a color. Prefer
KeyboardStudio styles and custom tokens so Avalonia upgrades cannot silently break the palette.

### Tests

- all three dictionaries expose exactly the same required keys;
- every referenced application token exists;
- application views contain no unapproved literal presentation colors;
- XAML compilation succeeds for the application and dialogs;
- existing key selection, error, and active-layer presentation tests remain green.

### Manual review

- compare the three palettes with the supplied wireframes for tonal character;
- tune contrast rather than copying sampled colors literally;
- inspect text, icons, borders, shadows, disabled controls, and focus indicators;
- inspect keyboard legends on normal, selected, active-layer, and error keycaps.

### Exit criteria

- White, Gray, and Black are visually distinct and internally consistent.
- All application surfaces switch without restart.
- No meaningful state relies on color alone.

## 7. P15.5 — Theme selection and application header

### Goal

Expose theme selection and move document commands into a compact, accessible header beside the
application title.

### Production changes

Add:

```text
AppearanceViewModel.cs
ThemeOptionViewModel.cs
```

`AppearanceViewModel` coordinates the settings store and theme service. It owns options, selection,
busy/error state, and immediate persistence. It must remain testable without a real Avalonia window.

Rebuild the top of `MainWindow.axaml` as one header:

- `KeyboardStudio` title;
- accessible vector File menu trigger immediately beside the title;
- concise document/layout status and dirty indicator;
- accessible vector Appearance trigger with White, Gray, and Black radio choices.

Move the existing New/Open/Import/Save menu entries into the icon-triggered menu without changing
commands or shortcuts. Show the full project path in a tooltip rather than as the primary header
label.

### Tests

- selecting each theme applies it immediately;
- one explicit selection performs one settings save;
- a save failure leaves the current theme active and exposes a non-modal warning;
- selected radio state follows the active theme;
- existing document commands and keyboard shortcuts remain bound;
- File and Appearance icon controls have accessible names and tooltips.

### Exit criteria

- The standalone top menu row no longer exists.
- The File trigger sits beside `KeyboardStudio` and exposes all existing document operations.
- Theme selection persists and works entirely by keyboard.

## 8. P15.6 — Current-layout startup and secondary new-document flow

### Goal

Make editing the current layout the default fresh-window workflow and remove the need to press
**Create**.

### Production changes

Add:

```text
IStartupLayoutLoader.cs
StartupLayoutLoader.cs
StartupLayoutResult.cs
StartupLayoutStatus.cs
```

Move host detection/import execution out of `MainWindowViewModel.ImportHostLayoutAsync` and into the
loader. The loader returns data and status only. `MainWindowViewModel` retains ownership of document
adoption, dirty state, profiles, provenance, diagnostics, and the untouched-startup guard.

Add explicit startup presentation states:

- loading current layout;
- current layout loaded;
- populated seed fallback;
- late result discarded because the user acted.

Remove `New from [template] [Create]` from the keyboard toolbar. Replace it with secondary document
commands:

- `Ctrl+N` creates a populated seed using the current geometry;
- File menu entries allow explicit ISO-105 or ANSI-104 creation;
- Import remains in the File menu;
- every replacement path continues through the unsaved-changes confirmation.

Do not block the first frame on host layout I/O and do not make the seed temporarily uneditable.

### Tests

- successful load returns an imported document and presentation status;
- unavailable, failed, and cancelled loads return safe results without throwing into the UI;
- a successful result replaces only the original clean, pathless startup project;
- edits, Open, Import, or New before completion prevent replacement;
- fallback is populated and immediately editable;
- startup import keeps correct provenance and target profiles;
- `Ctrl+N` and explicit template creation preserve confirmation behavior;
- no main-window binding references the removed Create control.

### Exit criteria

- A supported Linux fresh start settles on the detected host layout without user action.
- Unsupported or failed detection leaves a populated editable seed.
- User work cannot be overwritten by a late result.

## 9. P15.7 — UX hierarchy and accessibility refinements

### Goal

Finish the committed UX improvements that make the three themes useful rather than purely cosmetic.

### Production changes

- Collapse Diagnostics to a compact summary when it has no actionable items.
- Auto-expand on errors while preserving an explicit user expansion state.
- Apply the new action hierarchy to Build, Save/import commits, Cancel, uninstall, and ordinary
  utility actions.
- Replace overly small supporting text and targets where they impair readability or interaction.
- Make document, import, dirty, warning, error, and build status presentation semantic and
  consistent.
- Reduce the visual weight of secondary right-panel cards without changing their workflows.
- Preserve the keyboard as the largest and highest-contrast work surface.

Do not add the deferred inspector-tab redesign, zoom model, recent files, or window-state persistence
under this work item.

### Tests and review

- Diagnostics collapse/expand behavior and error-triggered expansion;
- command enablement and destructive confirmations remain unchanged;
- focus order follows header, editor toolbar, keyboard, inspector, diagnostics;
- all icon-only actions expose accessible names;
- status and validation remain understandable with color removed.

### Exit criteria

- Required actions are visually distinguishable from secondary actions.
- Empty diagnostics no longer consumes disproportionate editor space.
- Minimum-window and keyboard-only use remain practical in every theme.

## 10. P15.8 — Verification and documentation closure

### Automated gates

Run:

```bash
dotnet build KeyboardStudio.slnx -c Release
dotnet test KeyboardStudio.slnx -c Release --no-build --filter "Category=Unit|Category=Golden"
```

Also run the existing Linux and Windows packaging/startup gates on their applicable runners. Theme
work is application-wide and must not be treated as Linux-only.

### Manual visual matrix

Verify:

- White, Gray, and Black;
- main window and every dialog, menu, tooltip, and popup;
- minimum and normal window sizes;
- 100%, 150%, and 200% scaling;
- Linux and Windows packages;
- normal, hover, pressed, focus, disabled, selected, warning, error, and success states;
- keyboard-only selection of all document and appearance commands;
- no first-frame theme flash;
- no required press of **Create** on a fresh window.

Record tested operating systems and any accepted visual limitation in the release checklist or
release notes.

### Documentation

Update behavior after implementation in:

- [`README.md`](../README.md);
- [`ARCHITECTURE.md`](ARCHITECTURE.md);
- [`DECISIONS.md`](DECISIONS.md);
- [`IMPLEMENTATION-PROGRESS.md`](IMPLEMENTATION-PROGRESS.md);
- [`MVP-RELEASE-CHECKLIST.md`](MVP-RELEASE-CHECKLIST.md), or its successor release checklist.

### Exit criteria

- All automated gates are green on applicable platforms.
- The visual/accessibility matrix is recorded and has no unresolved release-blocking defect.
- Documentation describes shipped behavior rather than the plan.
- Every touched C# file passes the one-top-level-type-per-file audit.

## 11. Commit strategy

Keep architecture, infrastructure, palette work, and UI rearrangement independently reviewable.
Suggested commits:

```text
docs: plan application themes and shell
feat(app): persist local appearance settings
feat(app): apply saved theme before window creation
feat(app): add white gray and black theme resources
feat(app): add appearance selection to the application header
refactor(app): extract startup layout loading
feat(app): open fresh windows on the current layout
refactor(app): simplify editor hierarchy and diagnostics
test(app): close theme and startup regression coverage
docs: describe shipped application themes
```

Do not combine settings persistence, resource palettes, startup import extraction, and the complete
main-window rearrangement in one commit.

## 12. Risk register

### R15.1 — Theme dictionaries are incomplete

**Risk:** a popup, dialog, or state falls back to an unrelated Fluent color and becomes unreadable.

**Mitigation:** one required-token contract, dynamic resources only, view-literal audit, and a manual
surface/state matrix.

### R15.2 — Gray custom variant inherits the wrong control semantics

**Risk:** inputs or native decorations become dark while the gray palette expects dark text.

**Mitigation:** Gray inherits Light; Black alone inherits Dark; verify popups and window decoration on
both supported desktop platforms.

### R15.3 — Settings corruption blocks startup

**Risk:** a partial write makes the application unusable.

**Mitigation:** atomic replacement, default-on-read-failure, and no modal settings error during
startup.

### R15.4 — Theme selection contaminates project dirty state

**Risk:** changing appearance marks a keyboard project dirty or changes its serialized bytes.

**Mitigation:** application settings have no dependency on `ProjectDocumentService`; add an explicit
project-serialization regression test.

### R15.5 — Async startup overwrites user work

**Risk:** host import finishes after the user edits or opens a project.

**Mitigation:** retain the existing identity, dirty, and path guard in the document owner and test
each competing user action.

### R15.6 — Icon-only menus reduce discoverability or accessibility

**Risk:** users cannot identify or reach File and Appearance commands.

**Mitigation:** familiar vectors, tooltip text, automation names, visible focus, shortcuts, and
keyboard navigation tests.

### R15.7 — UI cleanup expands into a full redesign

**Risk:** theme work destabilizes established editing/build workflows.

**Mitigation:** commit only the refinements listed in `THEMING.md`; track tabs, zoom, recent files,
and large information-architecture changes separately.

