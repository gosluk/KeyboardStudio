# KeyboardStudio Diagnostics

KeyboardStudio diagnostics use stable codes intended for the desktop UI, automated tests, build
orchestration, and future command-line integrations. Messages may become clearer over time; callers
should use the code and severity when they need machine-readable behavior.

## Core project diagnostics

| Code | Severity | Meaning | Key linked |
|---|---|---|---|
| `KSP001` | Error | A physical key ID occurs more than once. | Yes |
| `KSP002` | Error | A scan code is outside the supported byte range. | Yes |
| `KSP003` | Error | A scan-code and extended-key identity occurs more than once. | No |
| `KSP101` | Error | The project display name is missing. | No |
| `KSP102` | Error | The project version is missing. | No |
| `KSP103` | Error | The project language or locale is missing. | No |
| `KSP104` | Info | The optional project description is empty. | No |
| `KSM001` | Error | A mapping refers to a physical key that does not exist. | Yes |
| `KSM002` | Error | A character output violates the one-Unicode-scalar policy. | Yes |
| `KSM003` | Error | A physical key has more than one mapping object. | Yes |
| `KSM100` | Warning | Outputs exist but the physical key has no logical-key assignment. | Yes |

## Windows compatibility diagnostics

| Code | Severity | Meaning | Key linked |
|---|---|---|---|
| `KSW001` | Error | The mapping cannot be represented by the supported Windows logical-key model. | Yes |
| `KSW002` | Error | The modifier combination is not supported by the Windows backend. | Yes |
| `KSW003` | Error | A character output cannot be represented by a v1 Windows character-table row. | Yes |
| `KSW004` | Error | A layer-specific special-key output cannot be represented by the scan-code mapping. | Yes |

Windows codes are declared in `KeyboardStudio.Windows`; Core does not depend on or interpret them.
The translator raises `WindowsTranslationException` with the complete structured issue list if any
Core structural or Windows compatibility error reaches the translation boundary.

## Windows build and artifact diagnostics

| Code | Severity | Meaning |
|---|---|---|
| `GEN_SOURCE` | Error | The selected backend could not translate or generate source for the project. |
| `PE_FILE` | Error | The linked artifact is missing. |
| `PE_HEADER` | Error | The output lacks a PE optional header. |
| `PE_TARGET` | Error | The selected target is unsupported by the PE verifier. |
| `PE_INVALID` | Error | The output is not a readable PE image or has malformed export data. |
| `PE_ARCH` | Error | The PE machine does not match the requested target. |
| `PE_DLL` | Error | The PE image lacks the DLL characteristic. |
| `PE_EXPORT` | Error | The exact `KbdLayerDescriptor` export is absent. |
| `PE_LOAD` | Error | The matching-host Windows loader rejected the DLL or export. |
| `MANIFEST_WRITE` | Error | The verified artifact manifest could not be written. |
| `REPRO_BUILD` | Error | The comparison build failed. |
| `REPRO_SOURCE` | Error | Repeated generation produced different source. |
| `REPRO_BINARY` | Error | Repeated builds produced different DLL hashes. |

Load-level verification records a structured not-run state rather than a diagnostic when the host is
not Windows or the process architecture cannot load the requested target.

## Linux XKB diagnostics

| Code | Severity | Meaning | Key linked |
|---|---|---|---|
| `KSL001` | Error | The template/physical-key pair has no XKB key-name mapping. | Yes |
| `KSL002` | Error | A logical or layer output cannot be represented as an XKB keysym. | Yes |
| `KSL003` | Error | Managed validation rejected identifiers, key names, levels, keysyms, ordering, or deterministic text. | Sometimes |
| `KSL004` | Warning/Error | `xkbcli` is missing; warning locally, error when external verification is required. | No |
| `KSL005` | Error | `xkbcli` rejected the isolated generated layout. | No |
| `KSL006` | Error | The XKB artifact or manifest could not be materialized safely. | No |

`KSL` diagnostics are declared in `KeyboardStudio.Linux`. External verification captures the exact
tool path, arguments, version, output, exit code, duration, and retained log without installing or
activating the generated layout.

## Layout import diagnostics

| Code | Severity | Meaning | Key linked |
|---|---|---|---|
| `KSI010` | Info | The source has no descriptive metadata for the layout, which is listed under its bare identifier. | No |
| `KSI011` | Info | The layout this host is configured to use could not be imported at startup, so whatever the editor was going to start from was kept. | No |
| `KSI020` | Warning | The definition carried more than one alternative group of outputs per key; only the primary group was imported. | No |
| `KSI021` | Warning | A construct the model cannot express — an action, redirection, or overlay — was read and ignored. | No |
| `KSI022` | Info | An unrecognized statement was skipped so the import could continue. | No |
| `KSI023` | Info | A composition rule was approximated by the nearest one the resolver implements. | No |
| `KSI024` | Error | A definition nested deeper than the resolver's cap. Nothing was imported. | No |
| `KSI025` | Warning | A composed definition could not be contributed — no source holds it, or it repeats one already being read. The rest was imported. | No |
| `KSI030` | Warning | An output sat on a modifier level beyond the four the model has and was dropped. | Yes |
| `KSI031` | Warning | A dead-key output was dropped; the layer was left unmapped rather than given a misleading character. | Yes |
| `KSI032` | Warning | An output has no equivalent in the model, so the layer was left unmapped. The message distinguishes an output the model cannot hold, such as a media key, from text that names no output at all. | Yes |
| `KSI033` | Info | The definition described a key the chosen template does not have, so the key was skipped. | Sometimes |
| `KSI034` | Error | The chosen physical keyboard template could not be loaded, so there was nothing to lay the layout onto. Nothing was imported. | No |

`KSI020` and `KSI021` name a key in their message but carry no key ID, because they are raised in the
symbols parser, which works in XKB key names before any template has been chosen to resolve them
against — there is no physical key to link to yet. `KSI033` carries one when the key resolved to a
physical identity the template lacks, and none when the name resolved to no physical key at all: the
key ID is what the editor jumps to, and the point of that second case is that there is nothing to
jump to.

`KSI` codes are declared in `KeyboardStudio.Core`, unlike `KSL` codes, which belong to
`KeyboardStudio.Linux`. Import loss is a property of the domain model rather than of any one
platform's file format: a future Windows source would drop dead keys and fifth levels for exactly the
same reason, and one shared range keeps two sources from assigning one number two meanings. The
wording is correspondingly source-neutral.

Import findings never block anything, because import produces a starting point rather than an
artifact. A `KSI024` or `KSI034` error means the import itself produced no project; it does not mark
a project invalid.

`KSI011` is the only import finding that can appear without the user having asked for an import. The
editor imports the host's own layout at startup, and that import declines quietly: it is `Info`
rather than `Warning` because nothing is wrong with the document the user ends up with, and the
entry exists only so the difference between "your host uses this layout" and "we could not read it"
is discoverable instead of invisible. It is cleared as soon as another document takes over, since by
then it explains something that is no longer on screen.

## Compatibility policy

- Existing code meanings are not reassigned.
- A retired diagnostic code remains reserved.
- New checks receive new codes instead of overloading an unrelated existing code.
- Only `Error` diagnostics block build orchestration.
- `Warning` and `Info` diagnostics remain visible but allow the build pipeline to proceed.

## Continuous editor validation

The desktop editor runs the composed in-memory rules after each successful mapping mutation and
after New or Open replaces the document. Core rules feed the editor diagnostics; selected-target
compatibility rules feed the build panel readiness/problems list. Both are deterministic, CPU-only
checks over the project model, so they run synchronously without a debounce. Rejected character
input and no-op edits do not trigger project validation because the domain did not change.

Continuous validation never invokes source generation, a native compiler, filesystem I/O, or the
Windows build environment. If later rules become expensive, they must move behind a debounced or
explicit validation boundary rather than making editor input block on build work.
