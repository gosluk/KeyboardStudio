# Keyboard Templates

This directory contains versioned physical keyboard geometry templates.

Initial built-in templates:

- `iso-105.json`
- `ansi-104.json`

The canonical structural contract is [`keyboard-template.schema.json`](keyboard-template.schema.json). A template contains stable physical key IDs, scan-code identity, extended-key state, and geometry. Project-specific logical mappings and character outputs remain in `.kbdproj` files and are intentionally excluded.

Geometry uses normalized keyboard units for `x`, `y`, `width`, and `height`. `unitWidth` and `unitGap` are reference rendering metrics in device-independent units; they are not stored pixel coordinates.

The ISO and ANSI files are structurally valid placeholders at P2.1 and intentionally keep an empty `keys` array. P2.3 and P2.4 populate the complete key sets. Runtime semantic validation, including duplicate IDs and scan-code conflicts, is implemented in P2.2.

See [`docs/KEYBOARD-TEMPLATE-FORMAT.md`](../docs/KEYBOARD-TEMPLATE-FORMAT.md) for the field-level contract and versioning rules.
