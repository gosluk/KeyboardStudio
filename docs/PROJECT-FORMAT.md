# KeyboardStudio Project Format

## Purpose

KeyboardStudio projects are persisted as human-readable JSON files with the `.kbdproj` extension.

The serialized format is a persistence contract. It is deliberately separated from the in-memory domain classes by persistence DTOs and explicit mapping.

## Independent version layers

The application release, outer document, and Core project schema are independent:

| Value | Purpose | Current value |
| --- | --- | ---: |
| KeyboardStudio application version | Desktop release identity | `0.1.0` |
| `documentSchemaVersion` | `.kbdproj` envelope, target profiles, provenance and derivation | `3` |
| `project.schemaVersion` | Platform-neutral Core project contract | `1` |

Changing the application version does not change either persistence schema. A schema version changes
only when its JSON contract requires migration.

Every Core project starts with an explicit integer schema version.

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
`KeyboardStudio.Windows`; Linux XKB generation uses `XkbLayoutMetadata` in
`KeyboardStudio.Linux`. Platform concepts do not leak into `KeyboardStudio.Core`.

Author metadata is intentionally omitted for now because generated resources do not consume it yet.

## Current application document shape

KeyboardStudio writes a versioned outer envelope. The envelope keeps platform-neutral project data
separate from backend profile settings while allowing both profiles to survive save/reopen.

```json
{
  "documentSchemaVersion": 2,
  "project": {
    "schemaVersion": 1,
    "metadata": {
      "name": "Swiss Polish",
      "description": "Swiss layout with Polish AltGr characters",
      "version": "1.0.0",
      "language": "de-CH"
    },
    "keyboard": {
      "id": "iso-105",
      "keys": []
    },
    "layout": {
      "mappings": []
    }
  },
  "targets": {
    "windowsX64": {
      "target": "windowsX64",
      "settings": {
        "layoutId": "kbdswisspolish",
        "layoutName": "Swiss Polish",
        "fileVersion": "1.0.0.0",
        "companyName": "Example"
      }
    },
    "linuxXkb": {
      "target": "linuxXkb",
      "settings": {
        "layoutId": "swiss_polish",
        "sectionId": "basic",
        "description": "Swiss Polish"
      }
    }
  },
  "importProvenance": {
    "sourceId": "linux-xkb",
    "layoutId": "pl",
    "variantId": "qwertz",
    "sourceLocation": "/usr/share/X11/xkb/symbols/pl",
    "sourceDescription": "Polish (QWERTZ)",
    "importedAtUtc": "2026-08-29T09:15:00+00:00"
  }
}
```

The target dictionary key must exactly match the entry's `target` discriminator. Missing known
profiles are recovered with application defaults; unknown profile entries remain a persistence
boundary concern and are not treated as Core domain data.

## Import provenance

`importProvenance` is present only on a document that began as an import, and is absent — not null,
not empty — on one that was authored. It records what the source said at the time and is never
re-read on load: the layout it names may since have been edited, upgraded, or uninstalled, and a
record of where something came from has to keep saying so even when the answer has changed.

Provenance lives in the envelope rather than in `project.metadata` because it is editor bookkeeping
rather than layout semantics. A `KeyboardProject` typed out by hand has no meaningful value for it,
and `KeyboardStudio.Core` would otherwise carry a concept only the application uses.

### Version 3 derivation baseline

Phase 14 adds an optional `layoutDerivation` sibling to `importProvenance` in the current version-3
wire format.

The provenance record answers "where did this document begin?". A derivation additionally answers
"what representable mappings were imported?" so KeyboardStudio can emit only later user changes
while inheriting the current system definition. Its conceptual fields are:

```json
{
  "layoutDerivation": {
    "projectInstallationId": "7c31d5f2a19e40a4b0ef64f01a295135",
    "sourceId": "linux-xkb",
    "sourceOrigin": "system",
    "baseLayoutId": "pl",
    "baseVariantId": "qwertz",
    "resolvedBaseSectionId": "qwertz",
    "importedAtUtc": "2026-08-29T09:15:00+00:00",
    "importFidelity": "exact",
    "baselineMappings": [],
    "sourceFingerprint": null,
    "includeChainFingerprint": null
  }
}
```

`projectInstallationId` is a generated 32-digit GUID representation that survives rename, Save As,
and copying the project. `baselineMappings` uses a dedicated persistence DTO representation of the
supported mapping state; it is not a second mutable `KeyboardLayout` in Core. Every entry also
records `isSafeToOverride`, derived from key-specific and layout-wide import loss. The snapshot is
immutable for the lifetime of the derivation and is replaced only by an explicit import-as-new from
a system-origin catalog entry. Documents without it remain valid and standalone-exportable but
cannot be installed as a derived system variant.

The optional source and include-chain fingerprints are reserved for import sources that can provide
stable fingerprints; the current XKB importer leaves them null. Host installation paths, hashes,
backups, and installed status are intentionally absent: they belong to host-local state, not a
portable `.kbdproj`.

## Envelope versions

| Version | Introduced |
| ---: | --- |
| `1` | The original envelope: project plus target profiles. |
| `2` | Added `importProvenance`. |
| `3` | Adds immutable `layoutDerivation` for import-derived user variants. |

`JsonKeyboardProjectDocumentStore` accepts every version from `FirstDocumentSchemaVersion` to
`CurrentDocumentSchemaVersion` and rejects anything outside that range, a newer version included: a
document written by a later release may mean something this one would misread.

An older envelope is migrated as raw JSON, one version per step, before the current DTO contract
reads it — the same rule the Core project migrations follow, and for the same reason, that a DTO
describes today's format and a historical document is by definition not in it. The `1` to `2` step
is registered even though it changes nothing, because `importProvenance` is optional and its absence
already reads as "not imported"; registering it keeps the chain gapless for the version after.

For compatibility, the application can still open the original direct Core schema-v1 document
shape shown below. It supplies default target profiles in memory and writes the current envelope on
the next save.

## Legacy direct Core project shape

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
`IKeyboardProjectStore` contract. `IKeyboardProjectDocumentStore` owns the outer envelope. The
original schema-v1 Core project format remains readable so pre-envelope files are not stranded.

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

## Persistence abstractions

```csharp
public interface IKeyboardProjectStore
{
    Task SaveAsync(KeyboardProject project, Stream destination);
    Task<KeyboardProject> LoadAsync(Stream source);
}
```

The implementation uses `System.Text.Json` in `KeyboardStudio.Persistence`, but JSON-specific concerns are contained behind DTOs, migration transforms, and mapping rather than leaking into `KeyboardStudio.Core`.

The desktop application uses the document-level contract:

```csharp
public interface IKeyboardProjectDocumentStore
{
    Task SaveAsync(KeyboardProjectDocument document, Stream destination);
    Task<KeyboardProjectDocument> LoadAsync(Stream source);
}
```
