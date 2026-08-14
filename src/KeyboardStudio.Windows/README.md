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
- generate deterministic native `VSC_VK`, key-name, modifier, character, and `KBDTABLES` source;
- emit `keyboard.c`, `keyboard.h`, `keyboard.def`, and `keyboard.rc` as one native source set;
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

## Phase 6 generation contract

The source generator follows the minimal WDK keyboard-layout ABI documented in
[`docs/WINDOWS-KBDTABLES-REFERENCE.md`](../../docs/WINDOWS-KBDTABLES-REFERENCE.md). It emits dense
primary scan tables, sentinel-terminated E0/E1 and key-name tables, four v1 modifier states,
two- or four-column UTF-16 character tables, a complete MVP descriptor, its exported entry point,
and deterministic resource metadata. `KeyboardStudio.Build` consumes this source set through its
Phase 7 MSVC/WDK toolchain integration and derives the output DLL name from the validated layout ID.
