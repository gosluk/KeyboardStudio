# Keyboard Template Format

KeyboardStudio keyboard templates describe **physical keyboard identity and geometry only**. They are separate from `.kbdproj` project files and must never contain project-specific character mappings, modifier-layer outputs, or editor state.

The current template schema version is `1`, represented in code by `KeyboardTemplateSchema.CurrentVersion` and structurally by [`templates/keyboard-template.schema.json`](../templates/keyboard-template.schema.json).

## Root object

A template version 1 document has these required fields:

| Field | Type | Meaning |
| --- | --- | --- |
| `schemaVersion` | integer | Template format version. Version 1 is the current format. |
| `id` | string | Stable machine-readable template ID, for example `iso-105`. |
| `name` | string | Human-readable template name. |
| `unitWidth` | number > 0 | Reference width for one keyboard unit in device-independent rendering units. |
| `unitGap` | number >= 0 | Reference gap between keys in device-independent rendering units. |
| `keys` | array | Physical key definitions. |

`id` is intentionally stable and should not be localized. `name` is display text and may change without changing identity.

The JSON Schema allows an empty `keys` array so schema-only fixtures and staged template authoring remain possible. Built-in template completeness is a semantic rule enforced by the template provider; the P2.1 ISO and ANSI files therefore remain empty until P2.3 and P2.4 populate them.

## Physical key object

Each item in `keys` contains:

| Field | Type | Meaning |
| --- | --- | --- |
| `id` | string | Stable physical key ID used to connect the template to project mappings. |
| `scanCode` | integer 0-255 | Base hardware scan-code value. |
| `extended` | boolean, optional | Whether the key uses the template's extended-key identity. Defaults to `false`. |
| `x` | number >= 0 | Left coordinate in normalized keyboard units. |
| `y` | number >= 0 | Top coordinate in normalized keyboard units. |
| `width` | number > 0 | Key width in keyboard units. |
| `height` | number > 0 | Key height in keyboard units. |

Coordinates are **not pixels**. A normal key is generally `1 x 1`; wider keys use values such as `1.25`, `1.5`, `1.75`, `2`, or `2.25`. The renderer later scales these logical units to the available Avalonia surface while preserving proportions.

## Example

```json
{
  "schemaVersion": 1,
  "id": "iso-105",
  "name": "ISO 105-key",
  "unitWidth": 54,
  "unitGap": 4,
  "keys": [
    {
      "id": "KeyA",
      "scanCode": 30,
      "x": 1.75,
      "y": 3,
      "width": 1,
      "height": 1
    }
  ]
}
```

## Structural versus semantic validation

The JSON Schema defines the structural contract: required fields, primitive types, numeric ranges, ID shapes, and the absence of unknown properties. Runtime template validation adds rules that JSON Schema alone does not express conveniently, including:

- supported `schemaVersion`;
- registered resource identity and rendering metrics;
- duplicate physical key IDs;
- invalid duplicate `(scanCode, extended)` identities;
- finite, positive geometry;
- expected key count for each built-in definition;
- conversion into the cached `PhysicalKeyboard` domain representation.

`KeyboardTemplateErrorCode` gives callers a stable machine-readable reason when semantic validation fails. A base scan code may appear twice only when the `extended` flag makes the two physical identities distinct.

## Runtime provider

`IKeyboardTemplateProvider` exposes the built-in catalog and loads a template by stable ID. `KeyboardTemplateProvider` uses `EmbeddedKeyboardTemplateContentSource` by default. The existing files under `templates/` are linked into `KeyboardStudio.Core` as embedded resources, so repository JSON remains the single source of truth rather than being copied into project-specific resource files.

Built-ins are loaded lazily. A successfully validated template is cached once as a private key array. Each call to `Load` returns a new `PhysicalKeyboard` with a defensive `Keys` list, so callers can edit project state without mutating the provider cache. `PhysicalKey` instances themselves use init-only properties and can safely be shared from the cached definition.

The provider registers these completeness expectations:

| Template | Expected key count |
| --- | ---: |
| `iso-105` | 105 |
| `ansi-104` | 104 |

Until P2.3 and P2.4 populate the built-in files, they remain enumerable but `Load` intentionally reports `IncompleteTemplate`. This keeps P2.2 independent from the physical-layout population work while ensuring incomplete built-ins cannot silently enter a project.

## Versioning rules

Template schema changes must be deliberate:

- compatible content additions should prefer optional fields with clear defaults;
- incompatible changes require a new integer `schemaVersion`;
- a provider must reject an unsupported future version rather than silently reinterpret it;
- built-in templates must state their schema version explicitly;
- template schema versioning is independent from `.kbdproj` project schema versioning.

There is no template migration pipeline in version 1. If template evolution later requires migration, it should be designed separately from project-file migrations.

## Separation from project mappings

Template JSON must not contain:

- characters or Unicode outputs;
- `Default`, `Shift`, `AltGr`, or `Shift+AltGr` mappings;
- logical-key remapping state;
- validation state;
- dirty/editor state;
- Windows build metadata.

Those belong to project/domain or platform-specific layers, not to physical keyboard geometry.
