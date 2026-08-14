# KeyboardStudio.Build

Build orchestration and target tool integration.

Responsibilities:

- detect whether the Windows native build environment is available;
- invoke project validation before build;
- coordinate artifact generation;
- invoke the native compiler/linker through `INativeCompiler`;
- return structured compiler diagnostics and artifact paths.

The build pipeline must keep source generation testable without requiring the native toolchain.

## Target backend direction (Phase 9)

The current orchestrator models the Windows pipeline directly as validation, generation, environment
resolution, and native compilation. Linux XKB output is deterministic text and has no native
compile/link step, so Phase 9 will introduce `IBuildBackend` plus an `IBuildBackendResolver` selected
by `BuildOptions.Target`.

```text
WindowsX64 / WindowsArm64 -> generate -> compile/link -> PE verify -> DLL
LinuxXkb                  -> generate -> write -> optional xkbcli verify -> symbols file
```

`INativeCompiler` remains a Windows-backend collaborator rather than becoming a fake universal
materializer. At the orchestration/UI boundary, results and stages use target-neutral artifact
terminology. See [`docs/LINUX-XKB.md`](../../docs/LINUX-XKB.md).

## Windows toolchain contract

`WindowsBuildEnvironment` reports structured host/tool diagnostics and resolves x64 or ARM64 MSVC,
Windows SDK/WDK, include, and library paths. Discovery uses developer-environment variables first,
then Visual Studio `vswhere` and Windows Kits registration instead of fixed installation paths.

`MsvcKeyboardCompiler` writes the generated four-file source set into a unique `BuildWorkspace`,
runs `cl.exe`, `rc.exe`, and `link.exe` with argument-list process APIs, and returns the deterministic
layout DLL name. A successful result has passed PE machine/DLL checks, exact export verification, and
a matching-host Windows loader smoke test when available. `CompilationResult` includes parsed MSVC
diagnostics, verification state, a versioned build manifest, output SHA-256, and retained workspace
paths. `BuildOptions.VerifyReproducibility` opt-in builds twice and compares exact sources plus DLL
hashes; MSVC linking uses `/Brepro`.

The default `KeepFailedBuild` cleanup policy removes `generated/` and `obj/` after success while
retaining `output/` and `logs/`. Failures and cancellations keep the complete workspace for
troubleshooting. `DeleteFailedBuild` removes failed/cancelled workspaces; `KeepAll` also preserves
successful intermediates.
