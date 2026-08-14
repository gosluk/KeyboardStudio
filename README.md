# KeyboardStudio

KeyboardStudio is a modern Avalonia-based editor for defining platform-neutral custom keyboard
layouts. The implemented backend produces native Windows keyboard-layout DLLs; the roadmap now adds
Linux XKB symbols-file generation before the target-aware build UI is finalized.

The repository contains the working editor/domain/persistence foundation and the Windows source and
native build backend. The implementation is focused on this core workflow:

1. Display a physical keyboard.
2. Select a key and define its mapping for supported modifier layers.
3. Save and load a KeyboardStudio project.
4. Validate the project.
5. Select an artifact target.
6. Generate a native Windows keyboard-layout DLL or, in planned Phase 9, a Linux XKB symbols file.

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

The Phase 4 validation pipeline composes platform-neutral metadata, physical-key, and mapping rules
with a Windows compatibility rule at the application boundary. The diagnostics panel displays
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
 `- KeyboardStudio.Linux       -> Build + Core (planned Phase 9)
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
  KeyboardStudio.Linux/        # planned Phase 9
  KeyboardStudio.Build/

tests/
  KeyboardStudio.Core.Tests/
  KeyboardStudio.Windows.Tests/
  KeyboardStudio.Linux.Tests/  # planned Phase 9
  KeyboardStudio.App.Tests/

templates/

docs/
  ARCHITECTURE.md
  DIAGNOSTICS.md
  IMPLEMENTATION-PLAN.md
  LINUX-XKB.md
  PROJECT-FORMAT.md
  WINDOWS-BUILD.md
  DECISIONS.md
```

## MVP scope

Implemented through Phase 7:

- visual ISO/ANSI keyboard representation;
- physical-key selection;
- physical key to logical-key mapping;
- `Default`, `Shift`, `AltGr`, and `Shift+AltGr` output layers;
- Unicode character outputs;
- JSON project persistence;
- project validation;
- Windows keyboard-table source generation;
- native Windows DLL compilation.

Planned before MVP:

- Linux XKB v1 symbols-file generation and verification (planned Phase 9).

Explicitly out of MVP scope:

- installers and registry installation;
- dead keys and chained dead keys;
- ligatures;
- macros;
- IMEs;
- runtime keyboard hooks;
- PowerToys-style remapping;
- importing arbitrary existing keyboard DLLs.
- automatic installation or activation of generated XKB layouts.

## References

- Avalonia documentation: https://docs.avaloniaui.net/
- Microsoft Windows keyboard-layout samples: https://github.com/microsoft/Windows-driver-samples/tree/main/input/layout
- libxkbcommon XKB text format: https://xkbcommon.org/doc/current/keymap-text-format-v1-v2.html
