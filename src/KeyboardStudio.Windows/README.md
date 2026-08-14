# KeyboardStudio.Windows

Windows keyboard-layout translation and native source generation.

Responsibilities:

- translate `KeyboardProject` into an internal Windows keyboard model;
- map scan codes to Windows virtual keys;
- translate generic modifier layers to Windows modifier states;
- separate primary and extended scan-code tables;
- build typed two- or four-column character tables;
- keep scan-only keys out of printable character rows;
- validate Windows-only logical-key and modifier compatibility behind a Core rule contract;
- publish stable `KSW` compatibility diagnostic codes;
- generate deterministic native keyboard-layout source;
- isolate all Windows keyboard-table knowledge from the Avalonia UI and core domain.

This project generates source; it does not own compiler process execution.

## Phase 5 translation contract

`WindowsLayoutTranslator` produces a deterministic `WindowsKeyboardLayout` before source generation.
Logical-key to virtual-key conversion is an explicit mapping; it does not depend on matching enum
names. Normal scan codes and extended scan codes are stored in distinct typed collections.

The v1 modifier table assigns modifier numbers to Default, Shift, Ctrl+Alt (AltGr), and
Shift+Ctrl+Alt (Shift+AltGr). Ctrl-only, Alt-only, Shift+Ctrl, and Shift+Alt combinations are explicit
invalid states. Character tables use two columns when only Default and Shift are populated, and four
columns when an AltGr output is present.

Letters, digits, punctuation, Space, and printable numpad keys can have character rows. Enter, Tab,
Backspace, function/navigation keys, locks, modifiers, Windows keys, Context Menu, and Numpad Enter
are scan-only. A matching Default `SpecialKeyOutput` on a scan-only key is represented by its scan-code
mapping. Layer-specific special-key remapping and character outputs on scan-only keys are rejected.

Native v1 character rows hold one UTF-16 code unit, so non-BMP scalar values require later ligature
support and currently produce `KSW003`. Unsupported translations throw `WindowsTranslationException`
with stable, key-linked diagnostics instead of omitting data.
