# KeyboardStudio Project Format

## Purpose

KeyboardStudio projects are persisted as human-readable JSON files with the `.kbdproj` extension.

The serialized format is a persistence contract. It is not required to mirror the in-memory domain classes exactly.

## Versioning

Every project starts with a schema version.

```json
{
  "schemaVersion": 1
}
```

Future versions must migrate older schemas explicitly rather than silently changing interpretation.

```text
v1 -> v2 -> v3
```

## Project metadata

General metadata is platform-neutral and belongs to the core project model:

- `name` is the user-facing display name;
- `description` is human-readable explanatory text;
- `version` is the user-managed project version and is independent of `schemaVersion`;
- `language` is a BCP 47 language/locale tag, with `und` meaning unspecified.

Windows-only layout identity is not part of `ProjectMetadata`. It is represented by `WindowsLayoutMetadata` in `KeyboardStudio.Windows` so Windows concepts do not leak into `KeyboardStudio.Core`.

Author metadata is intentionally omitted for now because generated resources do not consume it yet.

## Intended v1 document

```json
{
  "schemaVersion": 1,
  "metadata": {
    "name": "Swiss Polish",
    "description": "Swiss layout with Polish AltGr characters",
    "version": "1.0.0",
    "language": "de-CH"
  },
  "targets": {
    "windows": {
      "layoutId": "kbdsp",
      "layoutName": "Swiss Polish"
    }
  },
  "keyboard": {
    "template": "iso-105"
  },
  "mappings": {
    "KeyA": {
      "logicalKey": "A",
      "outputs": {
        "default": { "type": "character", "value": "a" },
        "shift": { "type": "character", "value": "A" },
        "altGr": { "type": "character", "value": "ą" },
        "shiftAltGr": { "type": "character", "value": "Ą" }
      }
    },
    "KeyE": {
      "logicalKey": "E",
      "outputs": {
        "default": { "type": "character", "value": "e" },
        "shift": { "type": "character", "value": "E" },
        "altGr": { "type": "character", "value": "ę" },
        "shiftAltGr": { "type": "character", "value": "Ę" }
      }
    }
  }
}
```

The `targets.windows` wire representation is part of the intended durable persistence contract. P1.3 will introduce persistence DTOs and explicitly map this target metadata instead of coupling the stored format to runtime domain types.

## Design rules

- Key mappings reference stable physical key IDs from a keyboard template.
- Physical geometry is not copied into each project unless a future custom-geometry feature requires it.
- Modifier names are platform-neutral.
- Windows implementation structures are never serialized into `.kbdproj`.
- Windows build metadata is kept separate from general project metadata.
- Output objects are typed so additional output categories can be added later.

## Persistence abstraction

```csharp
public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination);
    Task<KeyboardProject> LoadAsync(Stream source);
}
```

The initial implementation uses `System.Text.Json` in `KeyboardStudio.Persistence`. P1.3 replaces direct domain serialization with explicit DTO mapping.
