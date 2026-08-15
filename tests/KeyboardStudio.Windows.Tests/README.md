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
- structured Windows build-environment detection and resolution;
- isolated workspace creation and traversal protection;
- argument-safe process execution and cancellation;
- x64/ARM64 compile, resource, and link command construction;
- MSVC diagnostic parsing, per-tool/raw logs, diagnostic manifests, and cleanup policies;
- native compilation of US-like letters, AltGr Unicode, ISO-105, and special/extended-key projects.

Golden fixtures are copied to the test output and compared after newline normalization. Set
`KEYBOARDSTUDIO_UPDATE_GOLDENS=1` only during an intentional fixture refresh; review every resulting
source diff before committing it.

Native compilation tests are marked `Category=WindowsIntegration`. They return without invoking a
compiler during ordinary local runs when the Windows MSVC/SDK environment is unavailable, so the
cross-platform gate remains deterministic. Windows CI requires that environment and fails if it
cannot compile, PE/export-verify, load-check, and reproducibility-check every representative fixture.
