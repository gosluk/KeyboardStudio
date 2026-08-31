#!/usr/bin/env python3
"""Vendors a miniature, pinned XKB database into the Linux test fixtures.

The golden import tests need input that never changes. The host's own database changes with every
distribution update, so the layouts those tests import are copied here once, verbatim, and the
version they came from is recorded beside them. Re-run this script to move the fixtures to a newer
xkeyboard-config; the goldens are then expected to change, and the diff is the point.

    scripts/vendor-xkb-fixtures.py [--root /usr/share/X11/xkb] [--version 2.47]

Only the symbols files the vendored layouts actually reach are copied, each one whole: a trimmed
symbols file would no longer be the thing upstream ships, which is the only property that makes it
worth vendoring.
"""

from __future__ import annotations

import argparse
import datetime
import re
import shutil
import subprocess
import sys
from pathlib import Path

# What the goldens import. The layouts the plan names, plus the variants that exercise composition
# the basic sections do not: dead keys, alternate alphabet arrangements, and a variant that pulls
# in the keypad and no-break-space definitions.
TARGETS: list[tuple[str, str | None]] = [
    ("us", None),
    ("us", "intl"),
    ("pl", None),
    ("pl", "qwertz"),
    ("de", None),
    ("de", "nodeadkeys"),
    ("fr", None),
    ("fr", "oss"),
    ("al", None),
    ("al", "plisi"),
]

# Every import composes the layout onto this, the way `rules/evdev` does, so the fixtures need it
# and everything it reaches even though no golden names it.
COMMON_BASE: tuple[str, str | None] = ("pc", None)

INCLUDE = re.compile(r'\b(?:include|augment|replace|alternate)\s+"([^"]+)"')
SECTION = re.compile(r'(?P<flags>[a-z_ \t]*)xkb_symbols\s+"(?P<name>[^"]+)"\s*\{')


def sections(text: str) -> tuple[dict[str, str], str | None]:
    """Splits a symbols file into its sections, and names the default one."""
    found: dict[str, str] = {}
    default: str | None = None

    for match in SECTION.finditer(text):
        depth = 1
        index = match.end()
        while index < len(text) and depth > 0:
            if text[index] == "{":
                depth += 1
            elif text[index] == "}":
                depth -= 1
            index += 1

        name = match.group("name")
        found[name] = text[match.end():index]
        if "default" in match.group("flags") and default is None:
            default = name

    return found, default or next(iter(found), None)


def closure(root: Path, targets: list[tuple[str, str | None]]) -> set[str]:
    """The set of symbols files reachable from the targets, following includes only."""
    needed: set[str] = set()
    seen: set[tuple[str, str | None]] = set()
    pending = list(targets)

    while pending:
        file, section = pending.pop()
        if (file, section) in seen:
            continue
        seen.add((file, section))

        path = root / "symbols" / file
        if not path.is_file():
            sys.exit(f"'{path}' does not exist; is --root pointing at an XKB database?")
        needed.add(file)

        found, default = sections(path.read_text(encoding="utf-8"))
        body = found.get(section) if section else (found.get(default) if default else None)
        if body is None:
            sys.exit(f"'{file}' has no section named '{section or default}'.")

        for specification in INCLUDE.findall(body):
            included = specification.split("(", 1)
            pending.append((
                included[0].strip(),
                included[1].rstrip(")").strip() if len(included) > 1 else None))

    return needed


def registry(root: Path, layouts: set[str]) -> str:
    """The registry entries for the vendored layouts, lifted verbatim out of rules/evdev.xml."""
    source = (root / "rules" / "evdev.xml").read_text(encoding="utf-8")
    entries = []

    for match in re.finditer(r"[ \t]*<layout>.*?</layout>\n", source, re.DOTALL):
        name = re.search(r"<name>([^<]+)</name>", match.group(0))
        if name is not None and name.group(1) in layouts:
            entries.append(match.group(0))

    if len(entries) != len(layouts):
        sys.exit(f"Expected {len(layouts)} layouts in the registry; matched {len(entries)}.")

    return (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        '<!DOCTYPE xkbConfigRegistry SYSTEM "xkb.dtd">\n'
        '<xkbConfigRegistry version="1.1">\n'
        "  <layoutList>\n"
        f"{''.join(entries)}"
        "  </layoutList>\n"
        "</xkbConfigRegistry>\n")


def installed_version() -> str:
    for command in (["rpm", "-q", "--qf", "%{VERSION}", "xkeyboard-config"],
                    ["dpkg-query", "--showformat=${Version}", "--show", "xkb-data"]):
        try:
            result = subprocess.run(command, capture_output=True, text=True, check=True)
        except (OSError, subprocess.CalledProcessError):
            continue
        if result.stdout.strip():
            return result.stdout.strip()

    return "unknown"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("/usr/share/X11/xkb"))
    parser.add_argument("--version", default=None)
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).resolve().parent.parent
        / "tests" / "KeyboardStudio.Linux.Tests" / "Fixtures" / "Xkb")
    arguments = parser.parse_args()

    files = sorted(closure(arguments.root, [COMMON_BASE, *TARGETS]))
    symbols = arguments.output / "symbols"
    shutil.rmtree(symbols, ignore_errors=True)
    symbols.mkdir(parents=True, exist_ok=True)

    for file in files:
        shutil.copyfile(arguments.root / "symbols" / file, symbols / file)

    rules = arguments.output / "rules"
    rules.mkdir(parents=True, exist_ok=True)
    (rules / "evdev.xml").write_text(
        registry(arguments.root, {layout for layout, _ in TARGETS}), encoding="utf-8")

    version = arguments.version or installed_version()
    (arguments.output / "PROVENANCE.md").write_text(
        f"""# Vendored XKB fixtures

Copied verbatim from xkeyboard-config **{version}** on {datetime.date.today().isoformat()} by
`scripts/vendor-xkb-fixtures.py`, which is also how to move them to a newer version.

xkeyboard-config is distributed under an MIT/X11-style licence; see the upstream `COPYING`. These
files are test input, are not compiled into any shipped artifact, and are never written to.

`rules/evdev.xml` holds the registry entries for {', '.join(sorted({layout for layout, _ in TARGETS}))}
only, lifted out of the upstream file unchanged. `symbols/` holds every file the vendored layouts
reach through their includes, together with `symbols/{COMMON_BASE[0]}` and its own includes, which
every import composes underneath the layout:

{chr(10).join(f'- `symbols/{file}`' for file in files)}
""", encoding="utf-8")

    print(f"Vendored {len(files)} symbols files from xkeyboard-config {version}:")
    print("  " + " ".join(files))


if __name__ == "__main__":
    main()
