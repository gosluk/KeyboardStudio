#!/usr/bin/env python3
"""Regenerate src/KeyboardStudio.Linux/Translation/XkbKeysymTable.g.cs.

The table is the name-to-character half of XKB import: a symbols file names its outputs
with keysym mnemonics such as ``Aogonek``, and nothing on the running machine can be
relied on to translate them. The headers ship with X11 development packages and
``keysym-utf.c`` with libxkbcommon's sources; none is present on a normal desktop, and
requiring them would make import fail on exactly the machines it is meant to serve. So the
table is built here, at development time, from pinned copies under ``third_party/keysyms``
and committed.

Five upstreams, because no one of them is enough. ``keysymdef.h`` names the standard
keysyms and annotates most with the Unicode character they stand for. ``XF86keysym.h``
names the media keys, and ``Sunkeysym.h``, ``HPkeysym.h`` and ``ap_keysym.h`` the vendor
keys, none of which keysymdef.h mentions; each of the four was added because the corpus
test found keysyms without it. ``keysym-utf.c`` is the table libxkbcommon actually
consults, so where it and the headers disagree it, not the header, says what the user's
machine will produce.

DECkeysym.h is deliberately absent: no file of xkeyboard-config names a DEC keysym, and a
source nothing exercises is a source nothing would catch going wrong. If one ever appears,
the corpus test names it.

Run from the repository root:

    python3 scripts/generate-keysym-table.py

Or, in CI, to prove the committed file still matches its sources:

    python3 scripts/generate-keysym-table.py --check
"""

import argparse
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
KEYSYMDEF = ROOT / "third_party" / "keysyms" / "keysymdef.h"
XF86KEYSYM = ROOT / "third_party" / "keysyms" / "XF86keysym.h"
SUNKEYSYM = ROOT / "third_party" / "keysyms" / "Sunkeysym.h"
HPKEYSYM = ROOT / "third_party" / "keysyms" / "HPkeysym.h"
APKEYSYM = ROOT / "third_party" / "keysyms" / "ap_keysym.h"
KEYSYM_UTF = ROOT / "third_party" / "keysyms" / "keysym-utf.c"
OUTPUT = ROOT / "src" / "KeyboardStudio.Linux" / "Translation" / "XkbKeysymTable.g.cs"

# Kept beside the vendored copies in third_party/keysyms/README.md; repeated here so the
# generated header can record what it was built from without a second file read.
KEYSYMDEF_ORIGIN = (
    "xorgproto include/X11/keysymdef.h",
    "https://gitlab.freedesktop.org/xorg/proto/xorgproto",
    "04482cdee458445eab7c6a0b6d4ea64b74387401",
)
XF86KEYSYM_ORIGIN = (
    "xorgproto include/X11/XF86keysym.h",
    "https://gitlab.freedesktop.org/xorg/proto/xorgproto",
    "fcb7e9a1a0b593a44740d83b0babddd331fea830",
)
SUNKEYSYM_ORIGIN = (
    "xorgproto include/X11/Sunkeysym.h",
    "https://gitlab.freedesktop.org/xorg/proto/xorgproto",
    "2b9fcd0d9507be39ea2729f1cd747e4fe6af12eb",
)
HPKEYSYM_ORIGIN = (
    "xorgproto include/X11/HPkeysym.h",
    "https://gitlab.freedesktop.org/xorg/proto/xorgproto",
    "df4d063b74b504117f8a180c6f260d27db99c6c4",
)
APKEYSYM_ORIGIN = (
    "xorgproto include/X11/ap_keysym.h",
    "https://gitlab.freedesktop.org/xorg/proto/xorgproto",
    "09602b2130b3710bcca4d2707132bd47d4a832ef",
)
KEYSYM_UTF_ORIGIN = (
    "libxkbcommon src/keysym-utf.c",
    "https://github.com/xkbcommon/libxkbcommon",
    "1b9284d405a50ca1074ad3c5eba0137d3aad716b",
)

# #define XK_Aacute 0x00c1  /* U+00C1 LATIN CAPITAL LETTER A WITH ACUTE */
# #define XF86XK_AudioPlay 0x1008ff14  /* Start playing of audio > */
#
# The macro prefix is not the keysym name: XF86XK_AudioPlay is the keysym XF86AudioPlay, and
# that is the spelling a symbols file uses.
# Over half of XF86keysym.h writes its value through the _EVDEVK macro instead, which offsets
# a Linux kernel key code into the reserved evdev keysym range:
#
# #define XF86XK_MediaPlayPause _EVDEVK(0x0a4)  /* KEY_PLAYPAUSE */
DEFINE = re.compile(
    r"^#define\s+(XK|XF86XK|SunXK|hpXK|osfXK|apXK)_([A-Za-z0-9_]+)\s+"
    r"(?:(0x[0-9a-fA-F]+)|_EVDEVK\((0x[0-9a-fA-F]+)\))\s*(?:/\*(.*?)\*/)?\s*$")
EVDEV_MACRO = re.compile(r"^#define\s+_EVDEVK\(_v\)\s+\((0x[0-9a-fA-F]+)\s*\+\s*_v\)")
PREFIX = {
    "XK": "", "XF86XK": "XF86", "SunXK": "Sun", "hpXK": "hp", "osfXK": "osf", "apXK": "ap"}

# Every keysym from 0x01000100 up is the character's own code plus 0x01000000, a rule
# keysymdef.h states in its header and libxkbcommon implements. Seven of its own keysyms are
# deprecated aliases carrying no annotation, so without applying the rule they would import as
# unrepresentable despite naming a perfectly ordinary character.
UNICODE_KEYSYM_BASE = 0x01000000
UNICODE_KEYSYM_RANGE = range(0x01000100, 0x0110FFFF + 1)

# keysymdef.h annotates a keysym's character three ways, described in its own header
# comment: "U+0041 ..." for a one-to-one mapping, "<U+0020 ...>" for a keysym with the
# same character but narrower meaning (KP_Space), and "(U+2329 ...)" for a legacy mapping
# that is only approximate. All three still produce a character when a layout uses the
# keysym, so all three are taken; the distinction matters to Unicode, not to typing.
ANNOTATION = re.compile(r"^\s*(?:(<)|(\())?U\+([0-9A-Fa-f]{4,6})")

# { 0x01a1, false, 0x0104 }, /* Aogonek ... */
CODEPAIR = re.compile(
    r"^\s*\{\s*(0x[0-9a-fA-F]+)\s*,\s*(?:true|false)\s*,\s*(0x[0-9a-fA-F]+)\s*\}")


def read_header(path):
    """Every keysym the header names, in file order, with the character it claims for it."""
    entries = []
    evdev_base = None
    for line in path.read_text(encoding="utf-8").splitlines():
        # Read the evdev base from the header rather than hard-coding it, so that a change
        # upstream moves the keysyms with it instead of silently renumbering them here.
        macro = EVDEV_MACRO.match(line)
        if macro:
            evdev_base = int(macro.group(1), 16)
            continue

        match = DEFINE.match(line)
        if not match:
            continue
        name = PREFIX[match.group(1)] + match.group(2)
        if match.group(3) is not None:
            value = int(match.group(3), 16)
        else:
            if evdev_base is None:
                sys.exit(f"{path} uses _EVDEVK before defining it.")
            value = evdev_base + int(match.group(4), 16)
        comment = match.group(5) or ""
        annotation = ANNOTATION.search(comment)
        codepoint = int(annotation.group(3), 16) if annotation else None
        if codepoint is None and value in UNICODE_KEYSYM_RANGE:
            codepoint = value - UNICODE_KEYSYM_BASE
        entries.append((name, value, codepoint))
    return entries


def read_keysym_utf(path):
    """libxkbcommon's keysym-value-to-character table."""
    pairs = {}
    inside = False
    for line in path.read_text(encoding="utf-8").splitlines():
        if not inside:
            inside = "keysymtab[] = {" in line
            continue
        if line.strip().startswith("};"):
            break
        match = CODEPAIR.match(line)
        if match:
            pairs[int(match.group(1), 16)] = int(match.group(2), 16)
    return pairs


def build(entries, keysymtab):
    """Merge the sources, letting libxkbcommon settle any disagreement about a character."""
    # A keysym's character belongs to its value, not to the name it happens to be written
    # under. keysymdef.h annotates only the endorsed name of each value: `guillemetleft`
    # carries U+00AB and its deprecated alias `guillemotleft` carries nothing but a note
    # saying so. Both produce the same character on the user's machine, so the annotations
    # are pooled by value before any name is given a codepoint. Reading them per name
    # instead leaves every deprecated alias characterless, and layouts still write them.
    by_value = {}
    for name, value, codepoint in entries:
        if codepoint is None:
            continue
        if value in by_value and by_value[value] != codepoint:
            sys.exit(
                f"Two names for keysym 0x{value:04x} claim different characters: "
                f"U+{by_value[value]:04X} and U+{codepoint:04X} ({name}).")
        by_value.setdefault(value, codepoint)

    rows = []
    conflicts = []
    shadowed = []
    seen = set()
    disagreed = set()
    for name, value, _ in entries:
        # Headers are read in the order listed in main(), and the first definition of a name
        # wins. HPkeysym.h redefines XK_Ydiaeresis to a value that is not Y-with-diaeresis at
        # all, so a later-wins rule would corrupt a standard keysym with a vendor header's
        # mistake. Shadowed names are listed in the generated header rather than dropped
        # quietly, so a new one is noticed.
        if name in seen:
            shadowed.append((name, value))
            continue
        seen.add(name)

        codepoint = by_value.get(value)
        libxkbcommon = keysymtab.get(value)
        if libxkbcommon is not None and codepoint is not None and libxkbcommon != codepoint:
            # Reported once per value rather than once per name: the disagreement is between
            # the two sources about a keysym, and every alias of it inherits the same answer.
            if value not in disagreed:
                disagreed.add(value)
                conflicts.append((name, value, codepoint, libxkbcommon))
        if libxkbcommon is not None:
            codepoint = libxkbcommon
        rows.append((name, value, codepoint))
    return rows, conflicts, shadowed


def csharp_literal(name):
    return '"' + name + '"'


def render(rows, conflicts, shadowed):
    characters = sum(1 for _, _, codepoint in rows if codepoint is not None)
    lines = []
    add = lines.append

    add("// <auto-generated>")
    add("//     Generated by scripts/generate-keysym-table.py from the pinned sources under")
    add("//     third_party/keysyms. Do not edit by hand: run the script instead. CI regenerates")
    add("//     this file and fails on a difference.")
    add("//")
    for label, url, commit in (
            KEYSYMDEF_ORIGIN, XF86KEYSYM_ORIGIN, SUNKEYSYM_ORIGIN, HPKEYSYM_ORIGIN,
            APKEYSYM_ORIGIN, KEYSYM_UTF_ORIGIN):
        add(f"//     {label}")
        add(f"//         {url}")
        add(f"//         commit {commit}")
    add("//")
    add("//     The headers are licensed under the X11/MIT and HPND terms reproduced in each")
    add("//     vendored file, by The Open Group, Digital Equipment Corporation, The XFree86")
    add("//     Project, Sun Microsystems, Hewlett-Packard and Apollo Computer. The keysymtab in")
    add("//     libxkbcommon's keysym-utf.c is placed in the public domain by its author, Markus G.")
    add("//     Kuhn. Attribution is also recorded in third_party/keysyms/README.md.")
    add("//")
    add(f"//     {len(rows)} keysyms, {characters} of which name a character.")
    if conflicts:
        add("//")
        add("//     Where the two sources disagree on a keysym's character, libxkbcommon wins: it is")
        add("//     the table the user's own machine consults, so it decides what they will actually")
        add("//     type. The disagreements are listed so that bumping either source surfaces a new")
        add("//     one here rather than silently changing an imported layout:")
        for name, value, header, library in conflicts:
            add(f"//         {name} (0x{value:04x}): the header says U+{header:04X}, "
                f"libxkbcommon U+{library:04X}")
    if shadowed:
        add("//")
        add("//     Names defined more than once across the headers. The first definition wins, so")
        add("//     a vendor header cannot redefine a standard keysym; the ignored ones are:")
        for name, value in shadowed:
            add(f"//         {name}: a later header also defines it as 0x{value:08x}")
    add("// </auto-generated>")
    add("")
    add("using System.Collections.Frozen;")
    add("")
    add("namespace KeyboardStudio.Linux;")
    add("")
    add("/// <summary>")
    add("/// Every X11 keysym mnemonic, and the character it produces where it produces one.")
    add("///")
    add("/// Generated rather than read from the host: neither <c>keysymdef.h</c> nor libxkbcommon's")
    add("/// sources can be assumed installed on a machine running the application, and an import that")
    add("/// needed them would fail on ordinary desktops.")
    add("/// </summary>")
    add("public static class XkbKeysymTable")
    add("{")
    add("    /// <summary>Marks a keysym that names no character, such as <c>F1</c> or <c>dead_acute</c>.</summary>")
    add("    public const int NoCodepoint = -1;")
    add("")
    add("    /// <summary>")
    add("    /// What a keysym adds to a character's own code. Every keysym defined since Unicode is")
    add("    /// its character plus this, a rule keysymdef.h states and libxkbcommon implements, and")
    add("    /// it is how a keysym written as a bare number is read.")
    add("    /// </summary>")
    add(f"    public const uint UnicodeOffset = 0x{UNICODE_KEYSYM_BASE:08X};")
    add("")
    add("    private static readonly (string Name, uint Value, int Codepoint)[] Entries =")
    add("    [")
    for name, value, codepoint in rows:
        rendered = "NoCodepoint" if codepoint is None else f"0x{codepoint:04X}"
        add(f"        ({csharp_literal(name)}, 0x{value:08X}, {rendered}),")
    add("    ];")
    add("")
    add("    private static readonly FrozenDictionary<string, XkbKeysymEntry> ByName =")
    add("        Entries.ToFrozenDictionary(")
    add("            entry => entry.Name,")
    add("            entry => new XkbKeysymEntry(entry.Value, entry.Codepoint),")
    add("            StringComparer.Ordinal);")
    add("")
    add("    /// <summary>The whole table, keyed by keysym name.</summary>")
    add("    public static IReadOnlyDictionary<string, XkbKeysymEntry> All => ByName;")
    add("")
    add("    /// <summary>")
    add("    /// Whether the name is a keysym at all. Distinguishes a keysym the model cannot")
    add("    /// represent, such as <c>XF86AudioPlay</c>, from text that is not a keysym, which is")
    add("    /// the difference between a fidelity report saying \"not supported\" and \"not understood\".")
    add("    /// </summary>")
    add("    public static bool IsKnown(string name) => ByName.ContainsKey(name);")
    add("")
    add("    /// <summary>Gets the Unicode scalar the keysym produces, if it produces one.</summary>")
    add("    public static bool TryGetCodepoint(string name, out int codepoint)")
    add("    {")
    add("        if (ByName.TryGetValue(name, out var entry) && entry.Codepoint != NoCodepoint)")
    add("        {")
    add("            codepoint = entry.Codepoint;")
    add("            return true;")
    add("        }")
    add("")
    add("        codepoint = NoCodepoint;")
    add("        return false;")
    add("    }")
    add("}")
    add("")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="verify the committed file matches its sources instead of rewriting it")
    arguments = parser.parse_args()

    # keysymdef.h first: it is the standard, and the first definition of a name wins.
    entries = (read_header(KEYSYMDEF) + read_header(XF86KEYSYM) + read_header(SUNKEYSYM)
               + read_header(HPKEYSYM) + read_header(APKEYSYM))
    keysymtab = read_keysym_utf(KEYSYM_UTF)
    if not entries:
        sys.exit(f"No keysyms were found in {KEYSYMDEF}.")
    if not keysymtab:
        sys.exit(f"No keysym-to-character pairs were found in {KEYSYM_UTF}.")

    rows, conflicts, shadowed = build(entries, keysymtab)
    rendered = render(rows, conflicts, shadowed)

    if arguments.check:
        current = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if current != rendered:
            sys.exit(
                f"{OUTPUT.relative_to(ROOT)} is out of date with the sources in "
                f"third_party/keysyms. Run: python3 scripts/generate-keysym-table.py")
        print(f"{OUTPUT.relative_to(ROOT)} is up to date "
              f"({len(rows)} keysyms, {len(keysymtab)} libxkbcommon pairs).")
        return

    OUTPUT.write_text(rendered, encoding="utf-8")
    characters = sum(1 for _, _, codepoint in rows if codepoint is not None)
    print(f"Wrote {OUTPUT.relative_to(ROOT)}: {len(rows)} keysyms, "
          f"{characters} with a character, {len(conflicts)} source disagreements, "
          f"{len(shadowed)} shadowed names.")


if __name__ == "__main__":
    main()
