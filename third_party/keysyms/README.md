# Vendored keysym sources

These files are unmodified copies of upstream sources. They exist so that
[`scripts/generate-keysym-table.py`](../../scripts/generate-keysym-table.py) can build
`src/KeyboardStudio.Linux/Translation/XkbKeysymTable.g.cs`, the table that turns an XKB keysym name
such as `Aogonek` into the character it produces.

## Why they are vendored rather than read from the host

An XKB symbols file names its outputs with keysym mnemonics, and nothing translates them without a
table. That table could come from `keysymdef.h` and libxkbcommon's sources at run time, but neither
ships with a desktop system — they belong to development packages most users never install. Reading
them at run time would make layout import fail on precisely the machines it exists to serve, so the
table is generated here instead and committed.

Pinning them in the repository rather than fetching during generation keeps the build reproducible
and offline, and lets CI regenerate the table and diff it without a network dependency.

## What each file contributes

| File | Contributes |
| --- | --- |
| `keysymdef.h` | The standard keysyms, most annotated with the Unicode character they stand for. |
| `XF86keysym.h` | Media keys. Eleven files of xkeyboard-config use them, including `pc`, which nearly every layout composes. |
| `Sunkeysym.h` | Sun vendor keys, used by `sun_vndr/`. |
| `HPkeysym.h` | HP and OSF vendor keys, used by `hp_vndr/`. |
| `ap_keysym.h` | Apollo vendor keys, used by `digital_vndr/vt`. |
| `keysym-utf.c` | libxkbcommon's own keysym-to-character table. Where it and a header disagree, this one decides: it is what the user's machine consults, so it is what they will actually type. |

`DECkeysym.h` is deliberately absent. No file of xkeyboard-config names a DEC keysym, and a source
nothing exercises is a source nothing would catch going wrong. `XkbKeysymDecoderCorpusTests` fails
with the offending name if that ever stops being true.

Each of the four vendor headers was added because the corpus test found keysyms without it. None was
added speculatively.

## Provenance and licences

| File | Upstream | Pinned commit |
| --- | --- | --- |
| `keysymdef.h` | [xorgproto](https://gitlab.freedesktop.org/xorg/proto/xorgproto) `include/X11/keysymdef.h` | `04482cdee458445eab7c6a0b6d4ea64b74387401` |
| `XF86keysym.h` | [xorgproto](https://gitlab.freedesktop.org/xorg/proto/xorgproto) `include/X11/XF86keysym.h` | `fcb7e9a1a0b593a44740d83b0babddd331fea830` |
| `Sunkeysym.h` | [xorgproto](https://gitlab.freedesktop.org/xorg/proto/xorgproto) `include/X11/Sunkeysym.h` | `2b9fcd0d9507be39ea2729f1cd747e4fe6af12eb` |
| `HPkeysym.h` | [xorgproto](https://gitlab.freedesktop.org/xorg/proto/xorgproto) `include/X11/HPkeysym.h` | `df4d063b74b504117f8a180c6f260d27db99c6c4` |
| `ap_keysym.h` | [xorgproto](https://gitlab.freedesktop.org/xorg/proto/xorgproto) `include/X11/ap_keysym.h` | `09602b2130b3710bcca4d2707132bd47d4a832ef` |
| `keysym-utf.c` | [libxkbcommon](https://github.com/xkbcommon/libxkbcommon) `src/keysym-utf.c` | `1b9284d405a50ca1074ad3c5eba0137d3aad716b` |

The headers carry X11/MIT and HPND notices from The Open Group, Digital Equipment Corporation, The
XFree86 Project, Sun Microsystems, Hewlett-Packard and Apollo Computer; each notice is reproduced in
full at the top of the file it belongs to. The `keysymtab` table in `keysym-utf.c` is placed in the
public domain by its author, Markus G. Kuhn. All are attribution-only and impose no obligation
beyond keeping the notices, which vendoring the files verbatim does.

The same attribution appears in the header of the generated `XkbKeysymTable.g.cs`, so it travels with
the table rather than only with these sources.

## Updating

Replace the file, update its commit in the table above and in the matching `*_ORIGIN` constant in
`scripts/generate-keysym-table.py`, then regenerate:

```bash
python3 scripts/generate-keysym-table.py
```

Review the diff in the generated header before committing. It lists every keysym whose character the
two upstreams disagree about and every name one header redefines, so a bump that changes what an
imported layout produces shows up there rather than in a user's keyboard.
