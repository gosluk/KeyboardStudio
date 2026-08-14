# KeyboardStudio

KeyboardStudio is a modern Avalonia-based editor for defining custom keyboard layouts and compiling them into native Windows keyboard-layout artifacts.

The repository is currently in the architecture/bootstrap phase. The initial implementation is intentionally limited to the core workflow:

1. Display a physical keyboard.
2. Select a key and define its mapping for supported modifier layers.
3. Save and load a KeyboardStudio project.
4. Validate the project.
5. Generate native Windows keyboard-layout source.
6. Compile the generated source into a Windows keyboard-layout DLL.

## Architecture

The application uses a clean separation between the cross-platform editor and the Windows-specific compiler backend.

```text
KeyboardStudio.App
        |
        v
KeyboardStudio.Core <---- KeyboardStudio.Persistence
        ^
        |
KeyboardStudio.Windows
        ^
        |
KeyboardStudio.Build
```

The most important architectural rule is that `KeyboardStudio.Core` must not reference Avalonia, Windows APIs, the WDK, or MSVC.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the complete architecture.

## Repository structure

```text
src/
  KeyboardStudio.App/
  KeyboardStudio.Core/
  KeyboardStudio.Persistence/
  KeyboardStudio.Windows/
  KeyboardStudio.Build/

tests/
  KeyboardStudio.Core.Tests/
  KeyboardStudio.Windows.Tests/

templates/

docs/
  ARCHITECTURE.md
  PROJECT-FORMAT.md
  WINDOWS-BUILD.md
  DECISIONS.md
```

## Initial scope

Supported in the first implementation:

- visual ISO/ANSI keyboard representation;
- physical-key selection;
- physical key to logical-key mapping;
- `Default`, `Shift`, `AltGr`, and `Shift+AltGr` output layers;
- Unicode character outputs;
- JSON project persistence;
- project validation;
- Windows keyboard-table source generation;
- native Windows DLL compilation.

Explicitly out of initial scope:

- installers and registry installation;
- dead keys and chained dead keys;
- ligatures;
- macros;
- IMEs;
- runtime keyboard hooks;
- PowerToys-style remapping;
- importing arbitrary existing keyboard DLLs.

## References

- Avalonia documentation: https://docs.avaloniaui.net/
- Microsoft Windows keyboard-layout samples: https://github.com/microsoft/Windows-driver-samples/tree/main/input/layout
