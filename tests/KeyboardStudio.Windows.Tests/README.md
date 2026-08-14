# KeyboardStudio.Windows.Tests

Tests for the Windows translation/source-generation backend.

Coverage includes:

- scan-code to virtual-key translation;
- modifier translation;
- character table generation;
- Unicode output mappings;
- scan-only special keys and explicit unmapped outputs;
- structured failures for unsupported Windows mappings;
- deterministic generated source;

Golden/source snapshot coverage for real native tables begins in Phase 6.
