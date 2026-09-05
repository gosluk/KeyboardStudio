# KeyboardStudio

KeyboardStudio is a modern Avalonia-based editor for defining platform-neutral custom keyboard
layouts. The implemented backends produce verified native Windows keyboard-layout DLLs and portable
Linux XKB symbols components through a target-aware Avalonia build workflow.

The Phase 12 MVP implementation is complete. Release candidates are governed by the
[MVP release checklist](docs/MVP-RELEASE-CHECKLIST.md) and its aggregate CI gate.

The repository contains the working editor/domain/persistence foundation plus verified Windows and
Linux artifact backends. The implementation is focused on this core workflow:

1. Display a physical keyboard.
2. Select a key and define its mapping for supported modifier layers.
3. Save and load a KeyboardStudio project.
4. Validate the project.
5. Select an artifact target.
6. Generate a native Windows keyboard-layout DLL or a Linux XKB symbols file.

The current build is Linux-focused: the editor offers the Linux XKB target only. The Windows backend
is complete, referenced, and covered by CI, but its target is hidden from the Build panel. Start the
application with `KEYBOARDSTUDIO_TARGETS=all` to select it again. Hiding is presentation-only — a
document authored with a Windows profile keeps it through save and reload either way.

## Requirements

Two separate sets that are easy to confuse. One is what a machine needs to **compile**
KeyboardStudio and the artifacts it emits; the other is what an operating system must already
provide for the built application to **function**. A host can satisfy either one without the other.

### Tools required to compile

| To build | Requirement |
|---|---|
| The application, the libraries, and every test | A .NET 10 SDK, version 10.0.100 or a later 10.0 feature band, as pinned by [`global.json`](global.json). Every project targets `net10.0`. |
| A published, self-contained application | The `win-x64` or `linux-x64` runtime identifier. See [docs/PACKAGING.md](docs/PACKAGING.md). |
| A native Windows keyboard-layout DLL | Windows on x64, with the MSVC x64 toolchain (`cl.exe`, `link.exe` from `bin/Hostx64/x64`) and a Windows 10/11 SDK supplying `rc.exe`, the `ucrt`/`shared`/`um` headers, and the matching x64 libraries. Discovered through `VCToolsInstallDir`, `VSINSTALLDIR`, `vswhere.exe`, `WindowsSdkDir`, and `WindowsSDKVersion`. See [Windows build prerequisites](docs/WINDOWS-BUILD.md#prerequisites). |
| A Linux XKB symbols component | Nothing beyond the SDK. `xkbcli compile-keymap` is optional locally, where it adds external compilation verification on top of the managed structural checks; Linux CI always requires it. |

Editing, persistence, validation, and Linux XKB generation need no native development toolchain.

### OS requirements to function

| To use | Requirement |
|---|---|
| The editor itself | An x64 Windows or Linux desktop. |
| Startup on this host's current layout (Linux) | A readable canonical system XKB data root, normally `/usr/share/X11/xkb`. Without it the populated `us-basic` seed opens instead. |
| Generating and exporting an XKB bundle | Nothing further. This stays available on every host below. |
| Managed per-user installation — **Install**, **Update**, **Verify installed**, **Uninstall** | Every item in the list below. |

Managed per-user installation is a libxkbcommon user-configuration feature rather than a universal
Linux keyboard path, so all of the following must hold at once. Miss any one and the Linux
user-variant panel keeps **Generate bundle** enabled and disables the other four:

- **A Wayland session** (`XDG_SESSION_TYPE=wayland`) whose compositor compiles keymaps with
  libxkbcommon. An X11 session is export-only: the X server uses hard-coded XKB paths and does not
  treat the XDG directory as a replacement root.
- **libxkbcommon 1.11.0 or newer**, required for the `%S` system-section include that inherits the
  base layout. 1.12.2 or newer is the recommended baseline, because it also carries the
  canonical-root fallback some xkeyboard-config 2.45+ installations need.
- **`xkbcli` on `PATH`**, used to compile the keymap before and after writing anything. The shared
  library alone is not enough; it lives in a separate package on most distributions:

  | Distribution family | Install command |
  |---|---|
  | Fedora, RHEL | `sudo dnf install libxkbcommon-utils` |
  | Debian, Ubuntu, Linux Mint, Pop!_OS, KDE neon | `sudo apt install libxkbcommon-tools` |
  | Arch Linux, Manjaro | `sudo pacman -S libxkbcommon` (ships `xkbcli` in the library package) |
  | openSUSE | `zypper search --provides xkbcli`, then install what it names |

- **A canonical system XKB root** (`/usr/share/X11/xkb`) to inherit the imported base layout from.
- **Absolute, unsuspicious XDG paths**: `XDG_CONFIG_HOME` and `XDG_STATE_HOME`, or an absolute
  `HOME` for the `~/.config` and `~/.local/state` fallbacks. Installation writes only beneath
  `$XDG_CONFIG_HOME/xkb` and `$XDG_STATE_HOME/keyboardstudio/xkb`; a relative value disables it. An
  absent `~/.config/xkb` is normal, and the first approved installation creates it.
- **A desktop settings tool that reads libxkbregistry**, if the variant is to appear in the layout
  chooser on its own. This one is not a hard requirement: without it installation is still offered,
  with a warning, and KeyboardStudio reports registry discovery separately from keymap compilation.

The panel decides from a live probe of the running host, never from a distribution name, and shows
what it found in its capability and diagnostics lines. To check the same facts by hand:

```bash
printf 'session=%s wayland=%s\n' "$XDG_SESSION_TYPE" "$WAYLAND_DISPLAY"
command -v xkbcli && xkbcli --version
```

Nothing here is needed to edit, validate, save, or export. Installation is always explicitly
confirmed, never automatic, and never activates a layout. The full capability model, the reduced
modes, per-distribution expectations, and troubleshooting are in
[docs/LINUX-USER-XKB-VARIANTS.md](docs/LINUX-USER-XKB-VARIANTS.md#4-runtime-requirements-and-compatibility).

## Quick start

From the repository root, with a .NET 10 SDK installed:

```bash
dotnet restore KeyboardStudio.slnx
dotnet run --project src/KeyboardStudio.App/KeyboardStudio.App.csproj
```

On a Bash-capable host, `./scripts/run-app.sh` performs restore, compilation, and startup in one
command using a compatible installed .NET 10 SDK. Self-contained Windows and Linux publishing is
documented in
[docs/PACKAGING.md](docs/PACKAGING.md).

A fresh window opens on something editable and then settles onto the layout this host is already
configured to type with, where that can be read — on other hosts it keeps the populated `us-basic`
seed, a US layout on ISO-105 hardware with all 105 keys already mapped. Either way there is a
working layout on screen from the first frame and nothing to press first. Select a physical key,
change its logical key or any of the four layer values, then use **File > Save As** for the
`.kbdproj`, then build from the Build panel. `Ctrl+N` makes a new document using the geometry
already open, and the File menu offers the other geometries explicitly. Target profile edits are
stored in the same project document and restored when it is reopened. Normal builds only generate
artifacts; they never install or activate a keyboard layout.

Document commands live behind the File icon beside the `KeyboardStudio` title, and the Appearance
icon next to it offers three application themes — White, Gray, and Black. The choice applies to
every window, dialog, menu, and keycap immediately, and is remembered in a per-user `settings.json`
beneath the local application-data directory. It is never written into a `.kbdproj` and never marks
one dirty. A damaged or unreadable preference file starts the application in Gray rather than
failing, and is left untouched.

The Linux user-variant workflow starts from an imported system layout, emits only the user's
supported changes as a derived variant, and—after explicit confirmation—installs it transactionally
beneath the user's XDG XKB directory. It verifies proposed and installed roots, preserves unrelated
user content, and never activates the layout. Its architecture, host requirements, update safety,
and troubleshooting guide are in
[docs/LINUX-USER-XKB-VARIANTS.md](docs/LINUX-USER-XKB-VARIANTS.md).

See [Windows build prerequisites](docs/WINDOWS-BUILD.md#prerequisites) and
[Linux verification and safe manual installation](docs/LINUX-XKB.md#safe-manual-testing-and-installation).

Windows builds now verify PE architecture, DLL characteristics, the exact `KbdLayerDescriptor`
export, and—on a matching Windows host—loader resolution. Successful orchestration writes a hashed
build manifest, with an opt-in double-build reproducibility check.

Linux builds map ISO-105 and ANSI-104 physical identities to XKB key names, translate all four
modifier layers to typed keysyms, and write a deterministic `symbols/<layout-id>` component plus a
hashed manifest. Managed structural verification always runs; `xkbcli` compilation runs when the
tool is installed and is required by Linux CI. Builds never install or activate a layout.

The Build panel offers the Linux XKB target; retains an editable profile for
each target; checks common/target validation and required tools; reports only backend-owned stages;
and supports cancellation. Completed results expose generated C/XKB text, the output directory,
combined diagnostic/raw logs, and the canonical artifact path. Failures are grouped by project,
target, generation, toolchain, compiler/linker, and verification concern.

## Current editor and diagnostics workflow

The Phase 3 editor supports one selected physical key at a time and displays its physical ID,
scan code, logical-key assignment, and all four modifier outputs together. Choose the active
`Default`, `Shift`, `AltGr`, or `Shift + AltGr` layer to control the labels rendered on the keyboard.

Character outputs accept exactly one Unicode scalar value. Clear one layer, clear every output on
the selected key, or unmap its logical key with the controls in the details panel.

Projects use the `.kbdproj` format. The File menu and shortcuts provide:

- New — `Ctrl+N`;
- Open — `Ctrl+O`;
- Save — `Ctrl+S`;
- Save As — `Ctrl+Shift+S`.

Mapping changes mark the document as dirty. New and Open prompt to save or discard unsaved changes
before replacing the current document.

The editor validation pipeline composes platform-neutral metadata, physical-key, and mapping rules.
Selected-target compatibility is evaluated separately by the Build panel so a Windows-only error
does not block Linux output. The diagnostics panel displays
Info, Warning, and Error results using stable codes. Selecting a key-linked diagnostic selects and
highlights the affected physical key. Lightweight validation reruns after successful edits; only
errors block build orchestration.

See [docs/DIAGNOSTICS.md](docs/DIAGNOSTICS.md) for the code catalog, severity behavior, compatibility
policy, and continuous-validation boundary.

## Architecture

The application separates the cross-platform editor and build orchestration from target backends.

```text
KeyboardStudio.App
 |- KeyboardStudio.Core
 |- KeyboardStudio.Persistence -> Core
 |- KeyboardStudio.Build       -> Core
 |- KeyboardStudio.Windows     -> Build + Core
 `- KeyboardStudio.Linux       -> Build + Core
```

The most important architectural rule is that `KeyboardStudio.Core` must not reference Avalonia,
Windows APIs/WDK/MSVC, or Linux XKB/libxkbcommon concepts.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the complete architecture.

See [docs/IMPLEMENTATION-PLAN.md](docs/IMPLEMENTATION-PLAN.md) for the phased implementation plan, acceptance criteria, test gates, milestones, and risk register.

The White, Gray, and Black application themes, the local preference boundary, the application
header, and the current-layout startup path are specified in
[docs/THEMING.md](docs/THEMING.md); the ordered Phase 15 work and its test gates are in
[docs/THEMING-IMPLEMENTATION-PLAN.md](docs/THEMING-IMPLEMENTATION-PLAN.md).

## Repository structure

```text
src/
  KeyboardStudio.App/
  KeyboardStudio.Core/
  KeyboardStudio.Persistence/
  KeyboardStudio.Windows/
  KeyboardStudio.Linux/
  KeyboardStudio.Build/

tests/
  KeyboardStudio.Core.Tests/
  KeyboardStudio.Windows.Tests/
  KeyboardStudio.Linux.Tests/
  KeyboardStudio.App.Tests/

templates/
  seeds/

docs/
  ARCHITECTURE.md
  DIAGNOSTICS.md
  IMPLEMENTATION-PLAN.md
  LINUX-LAYOUT-IMPORT.md
  LINUX-USER-XKB-VARIANTS.md
  LINUX-XKB.md
  MVP-RELEASE-CHECKLIST.md
  PACKAGING.md
  VERSIONING.md
  PROJECT-FORMAT.md
  THEMING.md
  THEMING-IMPLEMENTATION-PLAN.md
  WINDOWS-BUILD.md
  DECISIONS.md
```

## MVP scope and limitations

The MVP supports:

- visual ISO/ANSI keyboard representation;
- a fully mapped seed layout in every new document;
- physical-key selection;
- physical key to logical-key mapping;
- `Default`, `Shift`, `AltGr`, and `Shift+AltGr` output layers;
- Unicode character outputs;
- JSON project persistence;
- project validation;
- Windows keyboard-table source generation;
- native Windows DLL compilation;
- PE/export/load verification and reproducibility manifests;
- Linux XKB v1 symbols-file generation, manifests, and optional local/required-CI `xkbcli`
  verification;
- target/profile selection, build readiness, backend-specific progress, cancellation, output actions,
  and categorized error/unverified-result presentation.

Explicitly out of MVP scope:

- installers and registry installation;
- dead keys and chained dead keys;
- ligatures;
- macros;
- IMEs;
- runtime keyboard hooks;
- PowerToys-style remapping;
- importing arbitrary existing keyboard DLLs;
- automatic activation of generated XKB layouts. Explicit transactional installation is available
  only for import-derived per-user variants; standalone artifacts remain export-only.

Additional release limitations:

- Windows artifact compilation is x64-only and must run on Windows with MSVC and a Windows SDK;
- Linux output is a symbols component, not a distribution-specific package or desktop registration;
- user-scoped XKB discovery and activation vary by compositor/desktop, and X11 has different path
  constraints from Wayland/libxkbcommon;
- supplementary-plane Unicode output is supported by XKB but not by the MVP Windows backend;
- project and target-profile settings are editable, but no installer, updater, or signing pipeline is
  included in the MVP.

## References

- Avalonia documentation: https://docs.avaloniaui.net/
- Microsoft Windows keyboard-layout samples: https://github.com/microsoft/Windows-driver-samples/tree/main/input/layout
- libxkbcommon XKB text format: https://xkbcommon.org/doc/current/keymap-text-format-v1-v2.html
