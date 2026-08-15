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

## Quick start

KeyboardStudio requires the .NET SDK version pinned in [`global.json`](global.json). From the
repository root:

```bash
dotnet restore KeyboardStudio.slnx
dotnet run --project src/KeyboardStudio.App/KeyboardStudio.App.csproj
```

On a Bash-capable host, `./scripts/run-app.sh` performs restore, compilation, and startup in one
command. Self-contained Windows and Linux publishing is documented in
[docs/PACKAGING.md](docs/PACKAGING.md).

The editor runs on x64 Windows and Linux desktops. Editing, persistence, validation, and Linux XKB
generation need no native development toolchain. A Windows DLL build additionally requires an x64
MSVC toolchain and Windows 10/11 SDK. Installing `xkbcli` is optional for local Linux generation and
adds external compilation verification; Linux CI always performs that verification.

Start with the ISO-105 template, select a physical key, choose its logical key, and fill any of the
four layer values. Use **File > Save As** for the `.kbdproj`, then select Windows x64 or Linux XKB in
the Build panel. Target profile edits are stored in the same project document and restored when it
is reopened. Builds only generate artifacts; they never install or activate a keyboard layout.

See [Windows build prerequisites](docs/WINDOWS-BUILD.md#prerequisites) and
[Linux verification and safe manual installation](docs/LINUX-XKB.md#safe-manual-testing-and-installation).

Windows builds now verify PE architecture, DLL characteristics, the exact `KbdLayerDescriptor`
export, and—on a matching Windows host—loader resolution. Successful orchestration writes a hashed
build manifest, with an opt-in double-build reproducibility check.

Linux builds map ISO-105 and ANSI-104 physical identities to XKB key names, translate all four
modifier layers to typed keysyms, and write a deterministic `symbols/<layout-id>` component plus a
hashed manifest. Managed structural verification always runs; `xkbcli` compilation runs when the
tool is installed and is required by Linux CI. Builds never install or activate a layout.

The Build panel selects Windows x64 or Linux XKB; retains an editable profile for
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

docs/
  ARCHITECTURE.md
  DIAGNOSTICS.md
  IMPLEMENTATION-PLAN.md
  LINUX-XKB.md
  MVP-RELEASE-CHECKLIST.md
  PACKAGING.md
  VERSIONING.md
  PROJECT-FORMAT.md
  WINDOWS-BUILD.md
  DECISIONS.md
```

## MVP scope and limitations

The MVP supports:

- visual ISO/ANSI keyboard representation;
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
- automatic installation or activation of generated XKB layouts.

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
