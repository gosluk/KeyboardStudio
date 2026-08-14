# Keyboard Templates

This directory contains versioned physical keyboard geometry templates.

Initial built-in templates:

- `iso-105.json` — complete ISO 105-key definition;
- `ansi-104.json` — structurally valid placeholder until P2.4.

The canonical structural contract is [`keyboard-template.schema.json`](keyboard-template.schema.json). A template contains stable physical key IDs, scan-code identity, extended-key state, and geometry. Project-specific logical mappings and character outputs remain in `.kbdproj` files and are intentionally excluded.

Geometry uses normalized keyboard units for `x`, `y`, `width`, and `height`. `unitWidth` and `unitGap` are reference rendering metrics in device-independent units; they are not stored pixel coordinates.

Scan-code values use the Windows Scan 1 make-code identity documented in [Microsoft's keyboard input overview](https://learn.microsoft.com/windows/win32/inputdev/about-keyboard-input). The JSON stores the base byte in `scanCode`; E0-prefixed keys set `extended` to `true`. The Pause key is the v1 special case: its E1 sequence is normalized to base byte `0x45` with `extended: true`, keeping it distinct from Num Lock while the Windows translation layer retains responsibility for exact E1 handling.

Stable IDs use familiar physical-key names (`KeyA`, `Digit1`, `Numpad1`, and so on). ISO-specific positions use `IntlHash` for the non-US hash/tilde position with scan code `0x2B`, and `IntlBackslash` for the additional ISO key with scan code `0x56`.

Runtime semantic validation, including duplicate IDs, duplicate scan identities, finite geometry, and built-in key counts, is implemented by `KeyboardTemplateProvider`.

See [`docs/KEYBOARD-TEMPLATE-FORMAT.md`](../docs/KEYBOARD-TEMPLATE-FORMAT.md) for the field-level contract and versioning rules.
