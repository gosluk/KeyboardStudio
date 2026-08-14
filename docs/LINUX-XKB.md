# Linux XKB Build Backend

## Status and scope

This document defines the planned Phase 9 Linux artifact backend. It is an architectural contract,
not a description of code that is already implemented.

The backend converts the same platform-neutral `KeyboardProject` used for Windows builds into a
classic XKB text format v1 symbols component. That component is the final Linux artifact. It is not a
native binary and does not need a compiler/linker step inside KeyboardStudio; libxkbcommon or X11
tooling assembles it with the host's other XKB components when the layout is loaded.

The initial backend generates and verifies files. It does not install, register, activate, or remove
a layout from a desktop session.

## Why generate a symbols component

XKB distinguishes a complete keymap from its keycodes, types, compatibility, symbols, and legacy
geometry components. A custom layout normally supplies an `xkb_symbols` component and reuses the
host's standard keycodes, types, compatibility definitions, and rules.

KeyboardStudio therefore emits:

```text
<xkb-root>/symbols/<layout-id>
```

rather than freezing host-specific components into a self-contained `xkb_keymap`. The output uses
text format v1 because it is the interoperable serialization choice for X11 and remains the safest
interchange format for Wayland clients. Format-v2-only features are outside the initial scope.

## Pipeline

```text
KeyboardProject + XkbLayoutMetadata + BuildTarget.LinuxXkb
                         |
                         v
          Core + XKB compatibility validation
                         |
                         v
               XkbLayoutTranslator
                         |
                         v
                XkbKeyboardLayout
                         |
                         v
               XkbSymbolsGenerator
                         |
                         v
       output/xkb/symbols/<layout-id>
                         |
                         v
      optional local / required CI xkbcli verification
```

Generation is a deterministic managed transformation and works on every host supported by the app.
`xkbcli` is an external verifier, not the producer of the artifact.

## Target selection

One build invocation selects exactly one target backend:

| Target | Materialization | Final artifact |
|---|---|---|
| `WindowsX64` | generate C, compile, link | x64 keyboard-layout DLL |
| `WindowsArm64` | generate C, compile, link | ARM64 keyboard-layout DLL |
| `LinuxXkb` | generate and write XKB text | `symbols/<layout-id>` |

Changing the target reuses the project metadata, physical template, logical mappings, and four output
layers. It selects different target metadata, compatibility rules, translators, and artifact stages.
No Windows compiler probing occurs for `LinuxXkb`.

## Target metadata

`XkbLayoutMetadata` contains only XKB build identity:

- layout/file ID, sanitized to the portable XKB identifier policy;
- section/variant ID, initially `basic` unless explicitly configured;
- display description used for `name[Group1]`.

It does not belong in `ProjectMetadata`. A project document may retain both `WindowsLayoutMetadata`
and `XkbLayoutMetadata`, while the Core aggregate stays usable without either. Target profiles are
persisted through the application/document settings boundary using stable target discriminators.

## Physical key identity

XKB symbols refer to symbolic key names, not Windows set-1 scan codes. The Linux backend uses the
stable template and physical key IDs as the translation key:

```text
(iso-105, KeyA)          -> <AC01>
(iso-105, Digit1)        -> <AE01>
(iso-105, IntlBackslash) -> <LSGT>
(ansi-104, Enter)        -> <RTRN>
(ansi-104, NumpadEnter)  -> <KPEN>
```

ISO-105 and ANSI-104 each receive explicit, table-driven coverage. The translator must not calculate
XKB names from `PhysicalKey.ScanCode` and must not store XKB key names in Core. An unsupported template
or key returns a stable `KSL` diagnostic associated with the physical key.

## Modifier layers and key types

The four Core layers map directly to XKB shift levels:

| Core layer | XKB level | Active modifier state |
|---|---:|---|
| `Default` | 1 | none |
| `Shift` | 2 | Shift |
| `AltGr` | 3 | LevelThree |
| `ShiftAltGr` | 4 | Shift+LevelThree |

The typed intermediate model selects a standard one-, two-, or four-level key type as required. It
uses an alphabetic type when the key's Caps Lock behavior requires it. If any mapping uses level 3 or
4, the symbols section includes the standard Right-Alt LevelThree switch. Missing levels inside a
required range are emitted as `NoSymbol`; trailing unused levels are omitted deterministically.

AltGr remains platform-neutral in Core. Windows translates it to the Windows Ctrl+Alt relationship,
while the XKB backend translates it to LevelThree. Neither representation leaks into the other.

## Keysyms

The translator distinguishes character output from logical/special keys:

- known non-character logical keys map to canonical names such as `Return`, `Tab`, `Left`, or `F1`;
- known characters may use an intentionally mapped canonical keysym name;
- all other Unicode scalar values use deterministic XKB Unicode keysym notation such as `U0105` or
  `U1F600`;
- no output uses `NoSymbol` where a placeholder is required.

The generator does not emit locale-dependent text or infer a keysym from the user's current layout.
Unsupported actions, multi-symbol sequences, dead keys, compose rules, and macros remain outside the
initial direct-mapping model.

## Deterministic output

The component contains:

- a generated-file comment with no timestamp or host path;
- a default named `xkb_symbols` section;
- `name[Group1]` from validated metadata;
- standard includes required for LevelThree behavior;
- key statements sorted by XKB key name;
- explicit types only where inference would be ambiguous.

Conceptual shape:

```text
default partial alphanumeric_keys modifier_keys
xkb_symbols "basic" {
    name[Group1] = "Example layout";

    key <AC01> { [ a, A, U0105, U0104 ] };
    key <RTRN> { [ Return ] };

    include "level3(ralt_switch)"
};
```

Exact formatting and include placement are golden-tested. Layout IDs and relative paths are validated
before any file is written; traversal and rooted paths are rejected.

## Verification and diagnostics

Managed validation always runs. On a host with `xkbcli`, the backend creates an isolated include root
and runs the equivalent of:

```text
xkbcli compile-keymap \
  --include <workspace>/xkb \
  --include-defaults \
  --test \
  --layout <layout-id> \
  --variant <section-id>
```

The custom include must precede the default includes. KeyboardStudio captures the executable, argument
list, version, working directory, stdout, stderr, exit code, and duration. Diagnostics are mapped into
the target-neutral build result and the raw log is retained.

If `xkbcli` is absent, generation may succeed with an explicit `Unverified` status and warning. Linux
CI installs the tool and treats verification failure as a failed build. Verification never targets an
active display server and never writes to `$XDG_CONFIG_HOME` or a system XKB directory.

## Artifact and manifest

The retained output is:

```text
output/
  xkb/
    symbols/
      <layout-id>
  build-manifest.json

logs/
  xkbcli.log                 # when verification ran
```

The manifest records project name, target, layout/section IDs, generator version, artifact path and
hash, verification status/tool version, and a manifest-only build timestamp. The symbols file itself
contains no time- or machine-dependent data.

## Test boundary

Unit and golden tests cover physical-key mapping, keysym translation, modifier levels, deterministic
formatting, Unicode, identifiers, and target dispatch. `XkbIntegration` tests use representative ISO
and ANSI projects and require `xkbcli --test` on Linux CI.

Normal tests do not activate the keymap. Interactive desktop testing and installation guidance are
release-documentation work, not part of generation.

## Authoritative references

- [libxkbcommon: XKB keymap text format v1 and v2](https://xkbcommon.org/doc/current/keymap-text-format-v1-v2.html)
- [libxkbcommon: custom configuration](https://xkbcommon.org/doc/current/custom-configuration.html)
- [libxkbcommon: keymap creation and format compatibility](https://xkbcommon.org/doc/current/group__keymap.html)
- [xkeyboard-config documentation](https://xkeyboard-config.freedesktop.org/doc/)
