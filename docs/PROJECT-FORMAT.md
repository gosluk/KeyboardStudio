# KeyboardStudio Project Format

## Purpose

KeyboardStudio projects are persisted as human-readable JSON files with the `.kbdproj` extension.

The serialized format is a persistence contract. It is deliberately separated from the in-memory domain classes by persistence DTOs and explicit mapping.

## Versioning

Every project starts with an explicit integer schema version.

```json
{
  "schemaVersion": 1
}
```

The current schema version is defined in one place by `KeyboardProjectSchema.CurrentVersion`. New `KeyboardProject` instances default to that value, and the persistence layer only writes the current schema version.

Loading validates `schemaVersion` before deserializing the rest of the document:

- the current version is accepted;
- a missing, non-integer, zero, or negative version is rejected as an invalid schema version;
- a future version is rejected with `ProjectLoadErrorCode.UnsupportedFutureSchema`;
- an older valid version is routed to `ProjectLoadErrorCode.LegacySchemaRequiresMigration` until the migration pipeline from P1.6 handles it.

Schema version 1 is the first project format, so there is currently no valid older schema to migrate. Future versions must migrate older schemas explicitly rather than silently changing interpretation.

Malformed JSON and structurally invalid current-version projects are reported through `ProjectLoadException` with a machine-readable `ProjectLoadErrorCode`. This gives the application a stable error boundary without requiring message parsing.

## Persistence DTO boundary

`JsonKeyboardProjectStore` serializes `KeyboardProjectDto`, not `KeyboardProject` directly. Explicit domain-to-DTO and DTO-to-domain mapping covers:

- project metadata;
- physical keyboard identity and geometry;
- layout mappings;
- logical keys and modifier layers;
- all currently supported output kinds.

Logical keys and modifier-layer names are represented by persistence strings rather than serializing domain enum values directly. This prevents a domain-model refactor from silently changing the stored JSON contract.

`KeyboardStudio.Core` contains no JSON polymorphism attributes. Output encoding is owned entirely by `KeyboardStudio.Persistence`, so the durable wire contract can evolve independently of runtime domain types.

## Output encoding

Every mapped output is an object with an explicit `kind` property. Runtime type names and serializer-specific discriminators such as `$type` are not part of the project format.

Supported version-1 shapes are:

```json
{ "kind": "character", "value": "ą" }
```

```json
{ "kind": "specialKey", "key": "space" }
```

```json
{ "kind": "none" }
```

The payload is strict:

- `character` requires a non-empty `value` and must not define `key`;
- `specialKey` requires a supported logical `key` and must not define `value`;
- `none` must define neither `value` nor `key`;
- unknown `kind` values are rejected as invalid projects.

A character value may itself be whitespace, such as a literal space. Scalar-count validation belongs to the editor/validation layer rather than the persistence transport.

## Project metadata

General metadata is platform-neutral and belongs to the core project model:

- `name` is the user-facing display name;
- `description` is human-readable explanatory text;
- `version` is the user-managed project version and is independent of `schemaVersion`;
- `language` is a BCP 47 language/locale tag, with `und` meaning unspecified.

Windows-only layout identity is not part of `ProjectMetadata`. It is represented by `WindowsLayoutMetadata` in `KeyboardStudio.Windows` so Windows concepts do not leak into `KeyboardStudio.Core`.

Author metadata is intentionally omitted for now because generated resources do not consume it yet.

## Current v1 document shape

```json
{
  "schemaVersion": 1,
  "metadata": {
    "name": "Swiss Polish",
    "description": "Swiss layout with Polish AltGr characters",
    "version": "1.0.0",
    "language": "de-CH"
  },
  "keyboard": {
    "id": "iso-105",
    "keys": [
      {
        "id": "KeyA",
        "scanCode": 30,
        "extended": false,
        "x": 0.75,
        "y": 1.0,
        "width": 1.0,
        "height": 1.0
      }
    ]
  },
  "layout": {
    "mappings": [
      {
        "keyId": "KeyA",
        "logicalKey": "a",
        "outputs": {
          "default": { "kind": "character", "value": "a" },
          "shift": { "kind": "character", "value": "A" },
          "altGr": { "kind": "character", "value": "ą" },
          "shiftAltGr": { "kind": "character", "value": "Ą" }
        }
      }
    ]
  }
}
```

Windows layout identity remains separate from the core aggregate and is not currently carried by the `IKeyboardProjectStore` contract. A target-specific document/settings boundary must preserve it without introducing a `KeyboardStudio.Core -> KeyboardStudio.Windows` dependency.

## Design rules

- Key mappings reference stable physical key IDs from a keyboard template.
- Modifier names are platform-neutral.
- Windows implementation structures are never serialized into `.kbdproj`.
- Windows build metadata stays separate from general project metadata.
- Output objects are typed so additional output categories can be added later.
- Runtime domain classes are not the persistence contract.
- Output kinds are stable persistence identifiers, not CLR type names.

## Persistence abstraction

```csharp
public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination);
    Task<KeyboardProject> LoadAsync(Stream source);
}
```

The implementation uses `System.Text.Json` in `KeyboardStudio.Persistence`, but JSON-specific concerns are contained behind DTOs and mapping rather than leaking into `KeyboardStudio.Core`.
