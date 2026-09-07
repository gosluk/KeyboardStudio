# KeyboardStudio.Build

Build orchestration and target tool integration.

Responsibilities:

- invoke project validation before build;
- resolve the build backend for the selected target;
- coordinate artifact generation;
- return structured diagnostics and artifact paths.

The build pipeline must keep source generation testable without requiring host tooling.

## Target backends

`BuildOrchestrator` runs platform-neutral validation and resolves exactly one `IBuildBackend` through
`IBuildBackendResolver`. Linux XKB output is deterministic text and has no native compile/link step,
so generation, writing, and verification all sit behind the target boundary in
`KeyboardStudio.Linux`.

```text
LinuxXkb -> generate -> write -> optional xkbcli verify -> symbols file
```

At the orchestration/UI boundary, results and stages use target-neutral artifact terminology. See
[`docs/LINUX-XKB.md`](../../docs/LINUX-XKB.md).

## Process execution

`IProcessRunner`/`ProcessRunner` is the shared, cancellable wrapper for invoking external tools with
argument-list process APIs. The XKB verification and installation-capability probes in
`KeyboardStudio.Linux` are its only consumers; keeping it here stops each backend growing its own
process plumbing.
