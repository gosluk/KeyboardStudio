# KeyboardStudio.Windows.Tests

Tests for the Windows translation/source-generation backend.

Coverage includes:

- scan-code to virtual-key translation;
- modifier translation;
- character table generation;
- Unicode output mappings;
- scan-only special keys and explicit unmapped outputs;
- structured failures for unsupported Windows mappings;
- deterministic four-file native source generation;
- primary, E0, and E1 scan-code tables and WDK flags;
- normal and extended native key names;
- `KBDTABLES`, `KbdLayerDescriptor`, module exports, and resource metadata;
- exact MinimalUs, AltGrUnicode, and IsoExample golden fixtures;

Golden fixtures are copied to the test output and compared after newline normalization. Set
`KEYBOARDSTUDIO_UPDATE_GOLDENS=1` only during an intentional fixture refresh; review every resulting
source diff before committing it.
