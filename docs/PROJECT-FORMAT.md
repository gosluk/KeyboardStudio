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

## Suggested v1 document

```json
{
  "schemaVersion": 1,
  "metadata": {
    "name": "Swiss Polish",
    "description": "Swiss layout with Polish AltGr characters",
    "version": "1.0",
    "language": "de-CH",
    "windowsLayoutId": "kbdsp"
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

## Design rules

- Key mappings reference stable physical key IDs from a keyboard template.
- Physical geometry is not copied into each project unless a future custom-geometry feature requires it.
- Modifier names are platform-neutral.
- Windows implementation structures are never serialized into `.kbdproj`.
- Output objects are typed so additional output categories can be added later.

## Persistence abstraction

```csharp
public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination);
    Task<KeyboardProject> LoadAsync(Stream source);
}
```

The initial implementation should use `System.Text.Json` in `KeyboardStudio.Persistence`.
