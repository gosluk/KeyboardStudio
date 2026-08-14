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

- the current version is accepted directly;
- a missing, non-integer, zero, or negative version is rejected as an invalid schema version;
- a future version is rejected with `ProjectLoadErrorCode.UnsupportedFutureSchema`;
- an older valid version is passed to `ProjectMigrationPipeline` before current-version DTO deserialization;
- if a required migration step is not registered, loading fails with `ProjectLoadErrorCode.LegacySchemaRequiresMigration`.

Migrations are persistence transformations, not domain transformations. Each `IProjectMigration` receives a JSON object for one historical schema version and advances it exactly one version. `ProjectMigrationPipeline` applies the chain in order and stamps `schemaVersion` after each successful step. This keeps version-specific compatibility logic out of `JsonKeyboardProjectStore` and ensures the final JSON is interpreted only by the current persistence DTO contract.

Schema version 1 is the first project format, so there is currently no valid older schema to migrate and no artificial version-0 migration is provided. When schema version 2 is introduced, its v1-to-v2 migration can be registered without changing the store's control flow.

Malformed JSON and structurally invalid current-version or migrated projects are reported through `ProjectLoadException` with a machine-readable `ProjectLoadErrorCode`. This gives the application a stable error boundary without requiring message parsing.

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

A character value may itself be whitespace, such as a literal space. Version 1 requires exactly one
Unicode scalar value. Supplementary-plane characters such as emoji are accepted as one scalar even
though UTF-16 represents them with a surrogate pair. Empty values, isolated surrogates, and
multi-scalar sequences (including decomposed grapheme sequences) are rejected. Multi-scalar
ligatures and macros remain explicitly deferred beyond the MVP.

## Project metadata

General metadata is platform-neutral and belongs to the core project model:

- `name` is the user-facing display name;
- `description` is human-readable explanatory text;
- `version` is the user-managed project version and is independent of `schemaVersion`;
- `language` is a BCP 47 language/locale tag, with `und` meaning unspecified.

Target-only layout identity is not part of `ProjectMetadata`. Windows uses `WindowsLayoutMetadata` in
`KeyboardStudio.Windows`; planned Linux XKB generation uses `XkbLayoutMetadata` in
`KeyboardStudio.Linux`. Platform concepts do not leak into `KeyboardStudio.Core`.

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

Target layout identities remain separate from the core aggregate and are not carried by the legacy
`IKeyboardProjectStore` contract. `IKeyboardProjectDocumentStore` provides a versioned outer envelope
with a nested Core project and a `targets` dictionary. Each entry has a stable target discriminator
and string settings, allowing Windows and XKB profiles to coexist without introducing backend
dependencies into Core. The original schema-v1 project format remains readable and unchanged.

## Design rules

- Key mappings reference stable physical key IDs from a keyboard template.
- Modifier names are platform-neutral.
- Platform implementation structures are never serialized into `.kbdproj`.
- Windows and XKB build metadata stay separate from general project metadata.
- Output objects are typed so additional output categories can be added later.
- Runtime domain classes are not the persistence contract.
- Output kinds are stable persistence identifiers, not CLR type names.
- Historical project schemas are migrated in persistence JSON before current DTO mapping.
- Each registered project migration advances exactly one schema version.

## Persistence abstraction

```csharp
public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination);
    Task<KeyboardProject> LoadAsync(Stream source);
}
```

The implementation uses `System.Text.Json` in `KeyboardStudio.Persistence`, but JSON-specific concerns are contained behind DTOs, migration transforms, and mapping rather than leaking into `KeyboardStudio.Core`.
