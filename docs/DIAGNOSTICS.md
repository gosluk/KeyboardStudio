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

Windows codes are declared in `KeyboardStudio.Windows`; Core does not depend on or interpret them.

## Compatibility policy

- Existing code meanings are not reassigned.
- A retired diagnostic code remains reserved.
- New checks receive new codes instead of overloading an unrelated existing code.
- Only `Error` diagnostics block build orchestration.
- `Warning` and `Info` diagnostics remain visible but allow the build pipeline to proceed.

## Continuous editor validation

The desktop editor runs the composed in-memory rules after each successful mapping mutation and
after New or Open replaces the document. The current Core and Windows compatibility rules are
deterministic, CPU-only checks over the project model, so they run synchronously without a debounce.
Rejected character input and no-op edits do not trigger project validation because the domain did
not change.

Continuous validation never invokes source generation, a native compiler, filesystem I/O, or the
Windows build environment. If later rules become expensive, they must move behind a debounced or
explicit validation boundary rather than making editor input block on build work.
