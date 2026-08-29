#!/usr/bin/env python3
"""Regenerate templates/seeds/us-basic.kbdproj from templates/iso-105.json.

The seed's physical keyboard is a verbatim copy of the ISO-105 geometry template, so this
script is the single point where the two can be brought back into agreement. The layout
half is the mapping table below: US basic (xkb ``us(basic)``) on ISO-105 hardware.

Run from the repository root:

    python3 scripts/generate-us-basic-seed.py
"""

import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
TEMPLATE = ROOT / "templates" / "iso-105.json"
SEED = ROOT / "templates" / "seeds" / "us-basic.kbdproj"

# Physical key ID -> (LogicalKey, default character, shift character).
# Sources: xkb symbols/us "basic" for the alphanumeric block and <BKSL>; symbols/pc for
# <LSGT>, which us(basic) does not define and which therefore falls through to the pc
# default of less/greater on ISO hardware.
CHARACTER_KEYS = {
    "Backquote": ("Backquote", "`", "~"),
    "Digit1": ("Digit1", "1", "!"),
    "Digit2": ("Digit2", "2", "@"),
    "Digit3": ("Digit3", "3", "#"),
    "Digit4": ("Digit4", "4", "$"),
    "Digit5": ("Digit5", "5", "%"),
    "Digit6": ("Digit6", "6", "^"),
    "Digit7": ("Digit7", "7", "&"),
    "Digit8": ("Digit8", "8", "*"),
    "Digit9": ("Digit9", "9", "("),
    "Digit0": ("Digit0", "0", ")"),
    "Minus": ("Minus", "-", "_"),
    "Equal": ("Equal", "=", "+"),
    "BracketLeft": ("LeftBracket", "[", "{"),
    "BracketRight": ("RightBracket", "]", "}"),
    "Semicolon": ("Semicolon", ";", ":"),
    "Quote": ("Quote", "'", '"'),
    "IntlHash": ("InternationalHash", "\\", "|"),
    "IntlBackslash": ("InternationalBackslash", "<", ">"),
    "Comma": ("Comma", ",", "<"),
    "Period": ("Period", ".", ">"),
    "Slash": ("Slash", "/", "?"),
}

for letter in "ABCDEFGHIJKLMNOPQRSTUVWXYZ":
    CHARACTER_KEYS[f"Key{letter}"] = (letter, letter.lower(), letter)

# Physical key ID -> LogicalKey, emitted as a single Default-layer special-key output.
SPECIAL_KEYS = {
    "Escape": "Escape",
    "Backspace": "Backspace",
    "Tab": "Tab",
    "Enter": "Enter",
    "CapsLock": "CapsLock",
    "Space": "Space",
    "PrintScreen": "PrintScreen",
    "ScrollLock": "ScrollLock",
    "Pause": "Pause",
    "Insert": "Insert",
    "Delete": "Delete",
    "Home": "Home",
    "End": "End",
    "PageUp": "PageUp",
    "PageDown": "PageDown",
    "ArrowUp": "ArrowUp",
    "ArrowDown": "ArrowDown",
    "ArrowLeft": "ArrowLeft",
    "ArrowRight": "ArrowRight",
    "NumLock": "NumLock",
    "NumpadDivide": "NumpadDivide",
    "NumpadMultiply": "NumpadMultiply",
    "NumpadSubtract": "NumpadSubtract",
    "NumpadAdd": "NumpadAdd",
    "NumpadEnter": "NumpadEnter",
    "NumpadDecimal": "NumpadDecimal",
    "ShiftLeft": "LeftShift",
    "ShiftRight": "RightShift",
    "ControlLeft": "LeftControl",
    "ControlRight": "RightControl",
    "AltLeft": "LeftAlt",
    "AltRight": "RightAlt",
    "MetaLeft": "LeftMeta",
    "MetaRight": "RightMeta",
    "ContextMenu": "ContextMenu",
}

for index in range(1, 13):
    SPECIAL_KEYS[f"F{index}"] = f"F{index}"

for digit in range(10):
    SPECIAL_KEYS[f"Numpad{digit}"] = f"Numpad{digit}"


def camel(name):
    return name[0].lower() + name[1:]


def main():
    template = json.loads(TEMPLATE.read_text(encoding="utf-8"))

    keys = []
    for key in template["keys"]:
        keys.append(
            {
                "id": key["id"],
                "scanCode": key["scanCode"],
                "extended": key.get("extended", False),
                "x": key["x"],
                "y": key["y"],
                "width": key["width"],
                "height": key["height"],
            }
        )

    mappings = []
    unmapped = []
    for key in template["keys"]:
        key_id = key["id"]
        if key_id in CHARACTER_KEYS:
            logical, default, shift = CHARACTER_KEYS[key_id]
            outputs = {
                "default": {"kind": "character", "value": default},
                "shift": {"kind": "character", "value": shift},
            }
        elif key_id in SPECIAL_KEYS:
            logical = SPECIAL_KEYS[key_id]
            outputs = {"default": {"kind": "specialKey", "key": camel(logical)}}
        else:
            unmapped.append(key_id)
            continue

        mappings.append(
            {"keyId": key_id, "logicalKey": camel(logical), "outputs": outputs}
        )

    if unmapped:
        print(f"error: no seed mapping for {', '.join(unmapped)}", file=sys.stderr)
        return 1

    seed = {
        "schemaVersion": 1,
        "metadata": {
            "name": "US basic",
            "description": (
                "US layout on ISO 105-key hardware. Starting point for a new project; "
                "edit any key or import a system layout to replace it."
            ),
            "version": "0.1.0",
            "language": "en-US",
        },
        "keyboard": {"id": template["id"], "keys": keys},
        "layout": {"mappings": mappings},
    }

    SEED.parent.mkdir(parents=True, exist_ok=True)
    SEED.write_text(
        json.dumps(seed, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"wrote {SEED.relative_to(ROOT)}: {len(keys)} keys, {len(mappings)} mappings")
    return 0


if __name__ == "__main__":
    sys.exit(main())
