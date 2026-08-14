# Windows Keyboard Table Reference

KeyboardStudio's native source shape follows Microsoft's `kbdus` sample in the
[Windows Driver Samples keyboard-layout collection](https://github.com/microsoft/Windows-driver-samples/tree/main/input/layout/kbdus).
The sample is used as an ABI and file-organization reference only. KeyboardStudio does not copy
its US-specific mappings, legacy generation comments, or tool-specific aliases.

The generated model maps to WDK structures as follows:

| KeyboardStudio concept | WDK representation |
| --- | --- |
| Non-extended physical scan code | Dense `USHORT` primary VSC-to-VK table |
| Extended physical scan code | Sentinel-terminated E0 `VSC_VK` table |
| E1 scan-code prefix | Sentinel-terminated E1 `VSC_VK` table; empty in the v1 model |
| Modifier bit and state mapping | `VK_TO_BIT` plus `MODIFIERS` |
| Printable output row | `VK_TO_WCHARS2` or `VK_TO_WCHARS4` |
| Character table group | Sentinel-terminated `VK_TO_WCHAR_TABLE` |
| Non-printable key display name | Normal or extended `VSC_LPWSTR` table |
| Complete layout descriptor | `KBDTABLES` |
| DLL entry point | `KbdLayerDescriptor` returning `PKBDTABLES` |

The v1 source generator intentionally emits four files: one C translation unit, one layout header,
one module-definition file, and one version-resource script. This mirrors the minimal useful WDK
sample boundary while leaving compiler project files and build-response files to the Phase 7
toolchain integration.

## Supported ABI subset

- Scan codes are one-byte set-1 values already validated by semantic translation.
- `PhysicalKey.Extended` represents an E0-prefixed key.
- The core model has no E1 discriminator, so the E1 table is emitted with only its required
  `{ 0, 0 }` sentinel.
- Modifier columns are Default, Shift, AltGr (Ctrl+Alt), and Shift+AltGr.
- Character output is one UTF-16 code unit. Unsupported non-BMP output is rejected before source
  generation.
- Dead keys, ligatures, locale-specific key-name lists, and multi-VK tables are not part of the MVP;
  their `KBDTABLES` fields are emitted as the WDK-defined null or zero values.

## Determinism policy

Generated files use LF newlines, invariant hexadecimal formatting, stable ordering, and no paths or
timestamps. Native characters are emitted as `0xNNNN` UTF-16 values, and absent character outputs
use the WDK `WCH_NONE` sentinel.
