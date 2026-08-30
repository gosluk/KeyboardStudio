# Vendored XKB fixtures

Copied verbatim from xkeyboard-config **2.47** on 2026-08-30 by
`scripts/vendor-xkb-fixtures.py`, which is also how to move them to a newer version.

xkeyboard-config is distributed under an MIT/X11-style licence; see the upstream `COPYING`. These
files are test input, are not compiled into any shipped artifact, and are never written to.

`rules/evdev.xml` holds the registry entries for al, de, fr, pl, us
only, lifted out of the upstream file unchanged. `symbols/` holds every file the vendored layouts
reach through their includes:

- `symbols/al`
- `symbols/de`
- `symbols/fr`
- `symbols/keypad`
- `symbols/kpdl`
- `symbols/latin`
- `symbols/level3`
- `symbols/nbsp`
- `symbols/pl`
- `symbols/us`
