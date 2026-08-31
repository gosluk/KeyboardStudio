# KeyboardStudio MVP release checklist

This checklist is the evidence map for Phase 12. A release candidate is eligible only when the
GitHub Actions **MVP release gate** passes for its commit and the manual desktop observations below
have no unresolved defect.

## Automated evidence

| Exit criterion | Evidence |
| --- | --- |
| App starts on x64 Windows and Linux | Self-contained package jobs run `--version`, start the real Avalonia desktop process for five seconds, and archive the same directory. |
| ISO-105 and ANSI-104 render correctly | Core template geometry tests and application geometry ViewModel tests. |
| Four modifier layers can be edited | Core editor tests plus `MvpEndToEndScenarioTests`, including Default, Shift, AltGr, and Shift+AltGr. |
| Save/load and target profiles are reliable | The end-to-end scenario saves the document envelope, reopens it, and checks both target profiles and Unicode mappings. Legacy direct schema-v1 files remain covered. |
| Invalid projects produce actionable diagnostics | The `ErrorPath` facet covers malformed/future files, missing profiles, target compatibility, toolchain/compiler/verifier failures, blocked output, and cancellation. |
| A valid project produces real Windows `KBDTABLES` source | Windows golden/source tests cover generated C, header, DEF, and resources. |
| Windows source compiles to a DLL | `WindowsIntegration` compiles representative ANSI, AltGr, ISO, and extended-key fixtures with MSVC x64. |
| The DLL passes verification | Windows integration requires x64 PE/DLL flags, exact `KbdLayerDescriptor` export, load resolution, and reproducible output. |
| The same project produces deterministic XKB v1 symbols | Linux unit/golden tests and the application end-to-end scenario verify stable symbols and manifests. |
| XKB passes `xkbcli` on Linux CI | `XkbIntegration` compiles representative ISO and ANSI output with the required external verifier. |
| Linux and Windows integration CI are green | The final gate depends on managed, XKB, Windows-native, and both packaging jobs. |
| Documentation matches behavior | README and project-format, architecture, Windows, Linux, packaging, versioning, diagnostics, theming, and testing guides describe the shipped workflow. |
| Every theme defines the whole colour contract | `ThemeResourceContractTests` holds all three dictionaries to `ApplicationThemeTokens`; `ApplicationXamlPresentationTests` rejects a view that names its own colour, a resource key KeyboardStudio does not define, and a theme token resolved statically. |
| Appearance never becomes project data | `AppearanceProjectIsolationTests` serializes a project either side of two theme changes and requires identical bytes and a still-clean document. |
| A damaged preference cannot block startup | `JsonApplicationSettingsStoreTests` covers missing, corrupt, unknown-theme, future-schema, blocked-path, and interrupted-write cases, and proves a failed replacement preserves the last complete file. |
| A fresh window needs no first press | `StartupLayoutLoaderTests` and `HostLayoutStartupImportTests` cover the loaded, unavailable, failed, cancelled, and discarded startup outcomes, and `MainWindowHeaderTests` proves no binding references the removed **Create** control. |
| The header keeps every command accessible | `MainWindowHeaderTests` and `ApplicationXamlHierarchyTests` hold the File menu's commands and shortcuts, the accessible name on every icon-only control, the focus order, the emphasis given to primary and destructive actions, and that no control theme drops the template it replaces. |

## Manual release observations

Automation proves startup and the artifact pipeline but cannot judge visual usability. Before
turning a release candidate into downloadable release assets:

- open the packaged application on a Windows 10/11 x64 desktop and one supported x64 Linux desktop;
- inspect ISO-105 and ANSI-104 geometry at normal and high-DPI scaling;
- edit all four layers, save, close, reopen, and confirm the selected profile values;
- generate one Windows DLL and one Linux symbols component from the same project;
- confirm failure messages remain readable at the packaged window size;
- verify the archives contain no user project, generated layout, signing secret, or retained build
  workspace.

### Appearance matrix

The theme work is application-wide, so a release candidate is also observed in each theme:

- White, Gray, and Black, on the main window and on every dialog, menu, tooltip, and popup;
- minimum (980x600) and normal window sizes, at 100%, 150%, and 200% display scaling;
- normal, hover, pressed, focus, disabled, selected, warning, error, and success states;
- no first-frame flash of an unrelated theme when the application starts;
- keyboard-only reach of every document and appearance command.

Recorded for the Phase 15 candidate, on Linux/X11 (KDE Plasma, Wayland session), from the packaged
self-contained build and the development build:

- all three palettes render their authored values exactly, sampled from the running application at
  the window, card, bezel, and selected-key surfaces;
- the import, unsaved-changes, and per-user XKB dialogs, the File and Appearance menus, and the
  diagnostics panel follow the active theme in all three;
- 980x600 keeps the header, keyboard, inspector, and collapsed diagnostics usable, and 150% and 200%
  scaling clip nothing;
- accepted limitation: window decorations are drawn by the desktop, not the application, so a title
  bar follows the desktop theme rather than the selected one. Black inherits Avalonia's Dark variant
  so that platforms which do honour an application preference receive it.

Record the tested OS versions and any waived limitation in the release notes. Windows installation,
Linux desktop registration/activation, signing, and installers remain outside the MVP and must not
be represented as completed by this checklist.
