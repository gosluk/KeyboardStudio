# Linux Layout Import

## Status and scope

This document specifies the Phase 13 layout-import subsystem. The design is adopted
([AD-019](DECISIONS.md) to [AD-023](DECISIONS.md)) and tracked as work items P13.1 and P13.3 to
P13.12 in [`IMPLEMENTATION-PLAN.md`](IMPLEMENTATION-PLAN.md). The seed (P13.1), the Core contract
in section 2 (P13.3), data-root discovery and registry reading in sections 3.2 and 3.3 (P13.4), and
the symbols lexer and parser in section 3.4 (P13.5) are implemented; include resolution, the rest of
the Linux pipeline, the dialog, and startup import are not.

Two related problems are addressed:

1. a new project currently opens as a template with **zero mappings**, which is not a usable
   starting point for editing;
2. there is no way to start from an existing layout, so every layout must be typed key by key.

The proposal covers the Linux/XKB source only. The abstractions are target-neutral so a Windows
source (`kbdutool` disassembly, `KLC` files, registry-installed DLLs) can be added later without
reshaping the editor.

Import is a **read-only** operation against the host's XKB data. It never writes to, activates,
registers, or removes anything from a system or session XKB root, matching the boundary already set
by [`LINUX-XKB.md`](LINUX-XKB.md).

---

## 1. Why a managed resolver rather than `xkbcli`

`xkbcli compile-keymap` would hand back a fully resolved, flat keymap and remove the need to
implement include resolution. It is rejected as the runtime mechanism because:

- it is **not installed** on the reference development host (xkeyboard-config 2.47 is present,
  libxkbcommon-tools is not), and it is optional on most desktop installs;
- [AD-017](DECISIONS.md) already establishes that KeyboardStudio treats `xkbcli` as an optional
  external verifier, never as the producer of a result. Making import depend on it would invert that;
- import must be deterministic and unit-testable on any host, exactly like generation
  (delivery principle 3.2).

`xkbcli` is instead used as a **CI conformance oracle**: where the tool is available, Linux
integration tests compile the same layout and diff the resolved key/level table against the managed
resolver's output. That gets the correctness benefit of libxkbcommon without a runtime dependency.

The corpus survey that makes a managed resolver tractable:

| Property | Measured on xkeyboard-config 2.47 |
|---|---:|
| `symbols` files | 144 |
| `xkb_symbols` sections | ~1340 |
| `include` statements | 1933 |
| non-default merge modes (`augment`/`replace`/`alternate`) | 6 |
| statements referencing `Group2` | 2 |
| `modifier_map` / `virtual_modifiers` statements | 143 / 2 |
| `actions[]`, `redirect`, `overlay` statements | 37 |
| distinct named keysyms in key statements | 2443 |
| distinct `dead_*` keysyms | 42 |

The grammar surface that actually matters for a four-level character layout is small. The bulk of the
work is the keysym vocabulary, which is table data rather than logic.

---

## 2. Target-neutral abstraction

Import produces a `KeyboardProject`, which is a Core concept, so the contract lives in Core. Core
gains no XKB knowledge: a layout is identified by opaque source/layout/variant strings.

```text
src/KeyboardStudio.Core/Layouts/Import/
  ILayoutImportSource.cs
  ILayoutImportCatalog.cs
  LayoutImportCatalog.cs
  ImportableLayoutDescriptor.cs
  ImportableLayoutReference.cs
  LayoutImportOptions.cs
  LayoutImportResult.cs
  LayoutImportReport.cs
  LayoutImportDiagnostic.cs
  LayoutImportDiagnosticCodes.cs
  LayoutImportFidelity.cs
  LayoutSourceOrigin.cs
```

```csharp
public interface ILayoutImportSource
{
    string Id { get; }                 // "linux-xkb"
    string DisplayName { get; }
    bool IsAvailable { get; }

    Task<IReadOnlyList<ImportableLayoutDescriptor>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<LayoutImportResult> ImportAsync(
        ImportableLayoutReference reference,
        LayoutImportOptions options,
        CancellationToken cancellationToken = default);
}
```

```csharp
public sealed record ImportableLayoutDescriptor(
    string SourceId,
    string LayoutId,                   // "pl"
    string? VariantId,                 // "qwertz", or null for the default section
    string DisplayName,                // "Polish (QWERTZ)"
    string? ShortDescription,          // "pl"
    IReadOnlyList<string> Languages,   // ISO 639
    IReadOnlyList<string> Countries,   // ISO 3166
    LayoutSourceOrigin Origin,         // User | System | File
    string SourceLocation);            // absolute path, for provenance and disambiguation
```

`ILayoutImportCatalog` aggregates sources and is the only type the ViewModels see. It mirrors
`IBuildBackendResolver`: the application composition root registers the concrete Linux source, and
presentation code stays free of XKB types (architecture §6.1).

`LayoutImportResult` carries the project **and** an honest fidelity report:

```csharp
public sealed record LayoutImportResult(
    bool Success,
    KeyboardProject? Project,
    string? SuggestedTemplateId,
    LayoutImportReport Report);

public sealed record LayoutImportReport(
    LayoutImportFidelity Fidelity,          // Exact | Reduced | Partial
    int KeysImported,
    int KeysSkipped,
    IReadOnlyList<string> ResolvedIncludeChain,
    IReadOnlyList<LayoutImportDiagnostic> Diagnostics);
```

`LayoutImportDiagnostic` follows the shape already used by `XkbDiagnostic` and `ValidationIssue`,
plus the layer the problem occurred on:

```csharp
public sealed record LayoutImportDiagnostic(
    ValidationSeverity Severity,
    string Code,                        // KSI###
    string Message,
    string? KeyId,
    ModifierLayer? Layer);
```

Reusing the existing `ValidationSeverity` lets the import report render through the existing
`DiagnosticsViewModel` path with a key-linked jump target.

### 2.1 As built

`LayoutImportOptions` carries the two choices a caller can make, both optional:

```csharp
public sealed record LayoutImportOptions(
    string? TemplateId = null,        // override the inferred geometry (§3.9)
    string? ProjectName = null)       // override the name derived from the layout
{
    public static LayoutImportOptions Default { get; } = new();
}
```

`ImportableLayoutDescriptor.ToReference()` builds the reference that re-fetches an entry, so a
descriptor and the reference that imports it cannot drift apart. A reference can still be
constructed by hand, with `SourceLocation` and no catalog entry, which is what **File > Import from
file…** needs.

`LayoutImportResult.Succeeded` / `.Failed` are the two ways to build a result, so the nonsensical
`Success = true, Project = null` state is not reachable by accident. A failed import stays an
ordinary result rather than an exception: host layout data is not the application's to trust, and an
unreadable layout is something to show in the dialog, not a fault to unwind the stack over.

`LayoutImportReport.Classify(keysSkipped, diagnostics)` derives the fidelity level so that every
source grades itself identically — any skipped key is `Partial`, any finding above `Info` is
`Reduced`, anything else is `Exact`.

`LayoutImportCatalog` skips a source whose `IsAvailable` is false without querying it, and lets a
failure from an available source propagate. A host with no layout database is ordinary and the
source says so up front; a source that claims to work and then does not is a real error, and
silently returning a shorter list would leave the user hunting for an installed layout with no
explanation on screen. Duplicate source IDs are rejected at registration, because those IDs are
written into saved documents as provenance.

The `KSI` codes live in Core rather than in the platform assembly that raises them — see
[`DIAGNOSTICS.md`](DIAGNOSTICS.md#layout-import-diagnostics) for the range and
[AD-019](DECISIONS.md) for why.

---

## 3. Linux implementation

All XKB knowledge stays in `KeyboardStudio.Linux`, alongside the existing `Translation/`,
`Generation/`, and `Verification/` folders.

```text
src/KeyboardStudio.Linux/Import/
  Discovery/
    IXkbDataRootLocator.cs
    XkbDataRootLocator.cs
    XkbDataRoot.cs
    IXkbActiveLayoutProbe.cs
    XkbActiveLayoutProbe.cs
    XkbActiveLayout.cs
  Registry/
    IXkbLayoutRegistryReader.cs
    XkbRulesRegistryReader.cs
    XkbRegistryEntry.cs
  Symbols/
    XkbSymbolsLexer.cs
    XkbSymbolsToken.cs
    XkbSymbolsTokenKind.cs
    XkbSymbolsParser.cs
    XkbSymbolsFile.cs
    XkbSymbolsSection.cs
    XkbSymbolsStatement.cs          (abstract; one file per derived statement type)
    XkbIncludeSpec.cs
    XkbMergeMode.cs
    IXkbIncludeResolver.cs
    XkbIncludeResolver.cs
    IXkbSymbolsResolver.cs
    XkbSymbolsResolver.cs
    ResolvedXkbSymbols.cs
    ResolvedXkbKey.cs
  Translation/
    IXkbKeysymDecoder.cs
    XkbKeysymDecoder.cs
    XkbKeysymTable.g.cs             (generated; see §6)
    IXkbKeyNameResolver.cs
    XkbKeyNameResolver.cs
    IXkbTemplateSelector.cs
    XkbTemplateSelector.cs
    XkbLayoutImporter.cs
  XkbLayoutImportSource.cs          (implements ILayoutImportSource)
```

Per `AGENTS.md`, every top-level type gets its own file; the tree above is written that way.

### 3.1 Pipeline

```text
XKB data roots (ordered)
        |
        v
rules/evdev.xml  -->  catalog of layout + variant descriptors
        |
   user selects one
        |
        v
symbols/<layout>  -->  lex  -->  parse  -->  section "<variant>"
        |
        v
include graph resolution (merge modes, cycle detection, depth cap)
        |
        v
ResolvedXkbSymbols: key name -> ordered Group1 levels
        |
        +--> XkbKeyNameResolver   : <AC01>  -> (template, "KeyA")
        +--> XkbKeysymDecoder     : aogonek -> CharacterOutput("ą")
        +--> level index          : 1..4    -> ModifierLayer
        |
        v
KeyboardProject + LayoutImportReport
```

### 3.2 Data-root discovery

Roots are searched in libxkbcommon's precedence order; the first root defining a symbols file wins,
and the catalog unions the layout lists so user layouts appear alongside system ones:

1. `$XKB_CONFIG_ROOT`, when set;
2. `${XDG_CONFIG_HOME:-$HOME/.config}/xkb` — user layouts, tagged `LayoutSourceOrigin.User`;
3. `/etc/xkb`;
4. `/usr/share/X11/xkb` and `/usr/local/share/X11/xkb` — tagged `System`.

`XkbDataRootLocator` takes the environment and a filesystem abstraction as constructor arguments so
the ordering is unit-testable without touching the host.

### 3.3 Registry reading

`rules/evdev.xml` (plus `evdev.extras.xml`) supplies display names, short descriptions, languages,
and countries. The file begins with `<!DOCTYPE xkbConfigRegistry SYSTEM "xkb.dtd">`, so the reader
**must** use `XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }`. This
is not optional hardening: the alternative resolves an external entity from a path the application
does not own.

A layout with no `<variantList>` yields one descriptor with `VariantId = null`, which resolves to the
file's `default` section. Layouts present in `symbols/` but absent from the registry are still
listed, with the file name as the display name and a `KSI010` informational diagnostic.

### 3.3.1 As built

`XkbDataRootLocator` and `XkbRulesRegistryReader` live under `Import/Discovery/` and
`Import/Registry/`, over two small host abstractions in `Import/Hosting/`: `IXkbEnvironment`, a
single `GetVariable`, and `IXkbFileSystem`, which exposes existence checks, a directory listing, and
`OpenRead`. `IXkbFileSystem` deliberately has no way to create or modify anything, so import's
read-only boundary is enforced by the interface rather than by convention.

Four resolution rules were settled while implementing section 3.2:

- `$XKB_CONFIG_ROOT` outranks the user's own directory. Whoever set the variable meant to redirect
  the whole search, and it names a database rather than the layouts this user wrote, so it is tagged
  `System`.
- A relative `XDG_CONFIG_HOME` is ignored rather than resolved. The base-directory specification
  calls it invalid, and resolving it would point the search at wherever the application happened to
  be launched from.
- Roots that resolve to the same path appear once, first occurrence winning. `XKB_CONFIG_ROOT` set
  to `/usr/share/X11/xkb` is common, and without this every layout in it would be listed twice.
- A host with no root at all yields an empty list, not an error. That is the ordinary state on
  Windows and in containers without X11 data.

For section 3.3, the reader emits the bare layout as an entry with `VariantId = null` alongside one
entry per variant, since the layout itself is importable and resolves to the symbols file's
`default` section. `evdev.xml` is read before `evdev.extras.xml` and the first description of a name
wins. A variant that lists no languages or countries inherits its layout's, because most variants
list none and without inheritance a search for "English" would find `us` but not `us(dvorak)`. An
entry with no `<name>` is skipped: the identifier is what addresses the symbols, so there is nothing
to import without it.

The `KSI010` diagnostic for layouts present in `symbols/` but absent from the registry is raised by
`XkbLayoutImportSource` (P13.9), which is where the two listings are unioned; the registry reader
reports only what the registry says.

### 3.4 Symbols parsing

The parser accepts the full statement vocabulary but only **consumes** what the Core model can
represent:

| Statement | Handling |
|---|---|
| `include "file(section)"` / `"file"` | resolved recursively |
| `xkb_symbols "name" { ... }` with `default` / `partial` / `hidden` flags | section selection |
| `name[Group1] = "..."` | project name and `XkbLayoutMetadata.Description` |
| `key <NAME> { [ a, A, x, X ] }` | imported |
| `key <NAME> { symbols[Group1] = [ ... ] }` | imported |
| `key <NAME> { type[Group1] = "..." , [ ... ] }` | symbols imported, type recorded and ignored |
| `key.type = "..."` | recorded and ignored |
| `key <NAME> { [ ... ], [ ... ] }` (Group2+) | Group1 imported, `KSI020` warning |
| `modifier_map`, `virtual_modifiers` | parsed, ignored, no diagnostic (never affects levels) |
| `actions[]`, `redirect`, `overlay` | parsed, ignored, `KSI021` warning |
| `replace`/`override`/`augment`/`alternate` prefixes | applied per §3.5 |

Comments are `//` to end of line. Unknown statements are skipped to the next `;` with a `KSI022`
informational diagnostic rather than aborting the import — the goal is a usable starting point, not a
conformant compiler.

### 3.4.1 As built

`XkbSymbolsLexer` is a static tokenizer that never fails: an unterminated string or key name ends at
the line break rather than swallowing the rest of the file, and an unknown character becomes an
`Unknown` token. Every judgement about well-formedness belongs to the parser, which recovers by
skipping one statement instead of refusing the file. Keywords stay plain identifiers because XKB has
no reserved words — `type` is a statement in one position and a keysym name in another.

`XkbSymbolsParser.Parse(path, text)` returns an `XkbSymbolsFile` carrying the sections and the
findings together, since parsing never throws and the diagnostics are the only record of the
difference between the file and what came back. `XkbSymbolsFile.DefaultSection` resolves a bare
`include "file"` the way libxkbcommon does: the section flagged `default`, or the first one when
none is.

`XkbKeyStatement` carries only the first group's keysyms. The model has one group, so keeping the
rest would mean inventing somewhere to put them; the parser drops them with `KSI020` instead.
`XkbIgnoredStatement` is kept rather than dropped so "understood and irrelevant" stays
distinguishable from "not understood" — conflating the two would either bury real gaps in noise or
hide them entirely. Key property names are matched without regard to case, because the corpus writes
the same property as both `virtualmods` and `virtualMods`.

`XkbMergeMode` was implemented here rather than with the resolver in P13.6: the prefix sits on `key`
and `include` statements alike, so the parser cannot represent `replace key <AD01>` without it.
`XkbIncludeSpec` stays in P13.6, and `XkbIncludeStatement.Specification` holds the include string
exactly as written — one string can name several sections joined by `+` or `|`, and splitting it
needs to know what the roots contain.

Two statement-skipping rules were settled against the real corpus. Skipping to a statement's end
counts braces, because `modifier_map Shift { Shift_L, Shift_R };` carries a block of its own and
halting at its closing brace would end the enclosing section early. A key statement, by contrast,
claims only a terminator sitting directly after its closing brace: scanning ahead for one would
swallow the next key whenever a file omits it.

Measured over the 199 files of the installed `xkeyboard-config`: 1,673 sections, 21,795 key
statements, 1,948 includes, 104 `KSI020` findings, 28 `KSI021` findings, and no `KSI022` at all. A
corpus test in `XkbSymbolsCorpusTests` holds that last number at zero, so an unrecognized statement
is treated as a gap in the grammar rather than as acceptable noise.

### 3.5 Include resolution and merge semantics

`XkbIncludeResolver` resolves an include spec against the ordered roots, supporting subdirectory
references such as `sun_vndr/us(sun_type6)` that appear in the real corpus. Rules:

- default and `override`: the including section's own statements win over the included ones, and a
  later include overrides an earlier one for the same key;
- `augment`: existing definitions win; only previously undefined keys are added;
- `replace`: the key definition is discarded and rebuilt;
- `alternate`: treated as `override` with a `KSI023` informational diagnostic (6 occurrences in the
  whole corpus; a faithful implementation is not worth the complexity yet).

Cycle detection keys on `(resolved absolute path, section name)` — **not** on the file alone. A file
legitimately includes other sections of itself (`pl(lefty)` includes `pl(basic)`), and a
file-granular visited set both breaks those layouts and hides genuine cycles. A depth cap of 16 with
a `KSI024` error guards pathological data.

The resolved include chain is retained in the report so an import can be explained and reproduced.

### 3.6 Level to layer mapping

The inverse of the export table in `LINUX-XKB.md`:

| XKB level | Core layer |
|---:|---|
| 1 | `Default` |
| 2 | `Shift` |
| 3 | `AltGr` |
| 4 | `ShiftAltGr` |
| 5+ | dropped, `KSI030` warning per key |

### 3.7 Keysym decoding

`XkbKeysymDecoder` is the inverse of the existing `XkbKeysymMapper`:

- `NoSymbol`, `VoidSymbol` -> `NoOutput`;
- `U0105`, `U1F600` and numeric `0x01000105` -> `CharacterOutput`;
- keysyms in the Unicode/Latin-1 direct ranges -> `CharacterOutput`;
- named keysyms with a Unicode equivalent (`aogonek`, `EuroSign`, `rightarrow`) -> `CharacterOutput`
  via the generated table;
- non-character function keys (`Return`, `Tab`, `Left`, `F1`) -> `SpecialKeyOutput(LogicalKey)`;
- `dead_*` -> `NoOutput` with a `KSI031` warning naming the key and layer. Dead keys are outside the
  MVP model (architecture §14) and this is the honest representation until they are modeled;
- anything unrecognized -> `NoOutput` with `KSI032`.

**Symmetry is enforced by test, not by convention.** For every `LogicalKey` and every character the
existing `XkbKeysymMapper` can emit, the decoder must return the original value. Both directions are
derived from the same table data so they cannot drift.

### 3.8 Physical key resolution

`XkbKeyNameResolver` inverts the `(templateId, keyId) -> <XKB name>` tables that
`XkbKeyNameMapper` already owns. Those tables are currently `private static`. The refactor is to
expose them once —

```csharp
public interface IXkbKeyNameMapper
{
    XkbKeyNameMappingResult Map(string templateId, string keyId);
    IReadOnlyDictionary<string, string> GetMappings(string templateId);   // keyId -> XKB name
}
```

— and build both directions from that single source, so import and export can never disagree about
`<LSGT>`. This preserves [AD-018](DECISIONS.md): XKB names are still absent from Core and are still
never derived from `PhysicalKey.ScanCode`.

XKB keys with no template counterpart (`<I120>`, media keys, `<FK13>`+) are skipped with a `KSI033`
informational diagnostic and counted in `KeysSkipped`.

### 3.9 Template selection

`XkbTemplateSelector` picks the geometry:

- resolved keys include `<LSGT>` -> `iso-105`;
- otherwise -> `ansi-104`;
- registry country hints break ties for layouts that define neither.

The result is a *suggestion*. The import dialog shows it and lets the user override, because the
registry does not record physical geometry and the heuristic will occasionally be wrong.

### 3.10 Logical key inference

`KeyMapping.LogicalKey` is derived deterministically:

1. if the level-1 output is a `SpecialKeyOutput`, use its `LogicalKey`;
2. else if the level-1 character is a single ASCII letter or digit, use the matching `LogicalKey`;
3. else fall back to the template key ID's conventional logical key (`Comma` -> `LogicalKey.Comma`);
4. else `LogicalKey.None`.

Rule 3 is what keeps a Dvorak or AZERTY import from labelling every key by its produced character
instead of its physical identity.

---

## 4. Provenance and round-tripping

An imported project records where it came from, in the **document envelope** rather than in
`ProjectMetadata` — provenance is editor bookkeeping, not layout semantics, and Core must not acquire
an XKB-shaped field:

```text
KeyboardProjectDocument
 |- documentSchemaVersion        (bumped; handled by the existing migration pipeline)
 |- project
 |- targets
 `- importProvenance
     |- sourceId                 "linux-xkb"
     |- layoutId                 "pl"
     |- variantId                "qwertz"
     |- sourceLocation           "/usr/share/X11/xkb/symbols/pl"
     |- sourceDescription        "Polish (QWERTZ)"
     `- importedAtUtc
```

Import also pre-fills the `XkbLayoutMetadata` target profile so the project can immediately be built
back out. The generated layout ID is **suffixed** (`pl` -> `pl-custom`), never reused verbatim: an
artifact named `symbols/pl` would shadow the distribution's own file if a user copied it into an XKB
root, which is precisely the failure mode `LINUX-XKB.md` warns about.

---

## 5. Non-empty startup

Fixing the empty-keyboard problem does not need the parser, and should ship before it.

**Guaranteed baseline.** A `us-basic` seed project is embedded in `KeyboardStudio.Core` next to the
existing geometry templates and becomes the content of a new document. This works on every host,
including Windows, macOS, and Linux boxes with no XKB data, and removes the empty-keyboard state
unconditionally.

**Host-aware improvement (Linux).** On startup the application resolves the host's configured layout
and imports it, replacing the seed if the document is still pristine. Detection is file- and
environment-based only, in order:

1. `XKB_DEFAULT_LAYOUT` / `XKB_DEFAULT_VARIANT`;
2. `Option "XkbLayout"` / `"XkbVariant"` in `/etc/X11/xorg.conf.d/00-keyboard.conf`;
3. `KEYMAP=` in `/etc/vconsole.conf`;
4. `us`.

No process is spawned. `localectl` and `gsettings` would each work but add a process dependency to
the startup path and are harder to test; both write the files above anyway. On the reference host all
three sources agree on `pl`, so a fresh session opens on the user's real layout.

The import runs asynchronously behind the seeded project so a slow or pathological symbols file
cannot delay the first frame, and any failure is silent apart from a diagnostics entry — a broken
host XKB database must never prevent the editor from opening.

---

## 6. Keysym table generation

The 2443 named keysyms in the corpus are table data, and neither `keysymdef.h` nor
`xkbcommon-keysyms.h` is guaranteed to be installed (neither is present on the reference host). The
table is therefore **generated at development time and committed**:

- `scripts/generate-keysym-table` reads X.org `keysymdef.h` and libxkbcommon's legacy keysym-to-Unicode
  table and emits `XkbKeysymTable.g.cs`;
- the generated file header records the upstream source and version;
- CI regenerates and diffs, so the table cannot silently drift from upstream.

Both upstreams are permissively licensed (MIT / X11 / HPND); attribution goes in the generated header
and in `templates/README.md`'s licensing note.

---

## 7. User interface

The host catalog is large — 99 layouts and 496 variants on xkeyboard-config 2.47, 595 selectable
entries in total — so it needs search, grouping, and a preview. That rules out a dropdown beside the
existing `New from [template] [Create]` control, and a permanent sidebar would cost roughly 260px of
main-window width for a control used once per project. It is therefore a modal
`ImportLayoutDialog`, reached from **File > Import layout…**, from an **Import…** button beside the
existing `New from` control, and from **File > Import from file…** for an arbitrary `symbols/` file.

Previewing before committing is a correctness requirement rather than a nicety: import is lossy (§3.7),
and the user needs to see which keys were dropped before an import replaces their project.

```text
+----------------------------------------------------------+
| Import layout                                            |
| Search [pol________]        [x] System   [x] User        |
| +--------------------+ +-------------------------------+ |
| | Polish          pl | | Preview      Geometry [iso-105 v] |
| |   > basic          | |  +---------------------------+ | |
| |     legacy         | |  | q w e r t y u i o p       | | |
| |     qwertz         | |  | a s d f g h j k l         | | |
| |     dvorak         | |  | z x c v b n m             | | |
| | Portuguese      pt | |  +---------------------------+ | |
| | Romanian        ro | |                               | |
| | ...                | | 104 keys imported             | |
| | (595 entries)      | | 6 dead keys dropped           | |
| |                    | | 2 keys skipped                | |
| +--------------------+ +-------------------------------+ |
|        [Import as new project] [Replace mappings] [Cancel] |
+----------------------------------------------------------+
```

"Replace mappings in current project" keeps the existing geometry, target profiles, and file path,
mutating only `KeyboardLayout` — the common case of "start from Polish and change three keys".

ViewModels — `LayoutImportViewModel`, `ImportableLayoutViewModel`, `LayoutImportReportViewModel` —
depend on `ILayoutImportCatalog` only. Both entry points route through the existing
`ConfirmDocumentReplacementAsync` dirty-document flow, and mutations go through `KeyboardEditor` so
the future undo/redo boundary stays intact (architecture §2.4).

---

## 8. Testing

`KeyboardStudio.Linux.Tests` gains:

- **Lexer/parser**: every statement kind, comments, malformed input, unterminated sections.
- **Include resolver**: default vs `augment` vs `replace`, cross-file, subdirectory, self-referencing
  sections, genuine cycles, depth cap.
- **Registry reader**: DTD is not resolved, malformed XML, missing variant lists, user/system merge.
- **Decoder symmetry**: exhaustive round-trip against `XkbKeysymMapper`.
- **Golden imports**: a small pinned copy of real `us`, `pl`, `de`, and `fr` symbols files is vendored
  as a test fixture (with attribution) so results do not depend on the host's installed
  xkeyboard-config version. Imported projects are snapshot-compared as JSON.
- **Full round trip**: import `us(basic)` -> `XkbSymbolsGenerator` -> re-import -> assert the Core
  models are equal. This is the strongest single correctness gate and covers key names, keysyms, and
  levels in one assertion.
- **Host soak test** (`XkbIntegration` trait, Linux CI): enumerate the real registry and import every
  layout and variant, asserting no exception and recording a fidelity histogram. ~1340 sections.
- **`xkbcli` conformance oracle** (`XkbIntegration`, skipped when the tool is absent): compile the
  same layout with `xkbcli compile-keymap` and diff the resolved key/level table against the managed
  resolver.

`KeyboardStudio.App.Tests` covers catalog listing and filtering, template override, import command
enablement, dirty-document confirmation, replace-mappings-in-place, and the startup seed/host-import
fallback chain against a fake catalog.

---

## 9. Proposed phasing

Work items are defined in [`IMPLEMENTATION-PLAN.md`](IMPLEMENTATION-PLAN.md). P13.2 belongs to the
same phase but covers target visibility rather than import.

| Item | Work | Ships independently |
|---|---|---|
| P13.1 | Embedded `us-basic` seed project; new documents are never empty | yes |
| P13.3 | Core import contract and `LayoutImportCatalog` | — |
| P13.4 | XKB data-root discovery and `rules/evdev.xml` registry reader | yes (catalog listing) |
| P13.5 | Symbols lexer and parser | — |
| P13.6 | Include resolver and merge semantics | — |
| P13.7 | Generated keysym table and `XkbKeysymDecoder` | — |
| P13.8 | `IXkbKeyNameMapper` bidirectional refactor and `XkbKeyNameResolver` | — |
| P13.9 | `XkbLayoutImporter`, template selection, fidelity report | yes |
| P13.10 | Import dialog, preview, replace-in-place, provenance persistence | yes |
| P13.11 | Host active-layout detection and startup import | yes |
| P13.12 | Golden, round-trip, soak, and `xkbcli` oracle tests | — |

P13.1 alone resolves the empty-keyboard problem and depends on nothing else, so it lands first.

---

## 10. Architecture decisions

Recorded in [`DECISIONS.md`](DECISIONS.md).

- **AD-019** — Layout import is a target-neutral Core contract with platform sources. Core defines
  `ILayoutImportSource`/`ILayoutImportCatalog` over opaque identifiers; XKB knowledge stays in
  `KeyboardStudio.Linux`; ViewModels never see XKB types.
- **AD-020** — XKB import uses a managed parser and include resolver, not `xkbcli`. `xkbcli` remains
  an optional CI conformance oracle, consistent with AD-017.
- **AD-021** — Import is lossy by design and reports its losses. Dead keys, groups beyond Group1,
  levels beyond 4, actions, and unmappable keysyms are dropped with key- and layer-linked
  diagnostics rather than failing the import.
- **AD-022** — XKB key-name tables are bidirectional and single-sourced. `XkbKeyNameMapper` owns one
  table used for both export and import; XKB names remain out of Core.
- **AD-023** — A new document is never empty. An embedded `us-basic` seed is the guaranteed baseline;
  the host's configured layout is imported over it on Linux when it can be resolved from the
  environment or `/etc` configuration files, without spawning a process.

[AD-024](DECISIONS.md) — hiding the Windows target in the UI — belongs to the same phase but is not
an import decision; see architecture section 2.6.

---

## 11. Explicitly out of scope

- writing, installing, activating, or removing any layout (unchanged from `LINUX-XKB.md`);
- dead keys, compose sequences, ligatures, and multi-symbol outputs — they are dropped on import and
  remain outside the domain model;
- groups 2-4, key types, `modifier_map`, virtual modifiers, and XKB actions;
- geometry import: physical layout still comes from `iso-105` / `ansi-104` templates;
- importing Windows KLC files or installed layout DLLs; the Core abstraction admits them later.

---

## 12. References

- [libxkbcommon: XKB keymap text format v1 and v2](https://xkbcommon.org/doc/current/keymap-text-format-v1-v2.html)
- [libxkbcommon: custom configuration](https://xkbcommon.org/doc/current/custom-configuration.html)
- [libxkbcommon: include resolution and search paths](https://xkbcommon.org/doc/current/group__include-path.html)
- [xkeyboard-config documentation](https://xkeyboard-config.freedesktop.org/doc/)
- [`LINUX-XKB.md`](LINUX-XKB.md) — the generation counterpart of this pipeline
