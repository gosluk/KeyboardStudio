# KeyboardStudio.Build

Build orchestration and native toolchain integration.

Responsibilities:

- detect whether the Windows native build environment is available;
- invoke project validation before build;
- coordinate artifact generation;
- invoke the native compiler/linker through `INativeCompiler`;
- return structured compiler diagnostics and artifact paths.

The build pipeline must keep source generation testable without requiring the native toolchain.

## Windows toolchain contract

`WindowsBuildEnvironment` reports structured host/tool diagnostics and resolves x64 or ARM64 MSVC,
Windows SDK/WDK, include, and library paths. Discovery uses developer-environment variables first,
then Visual Studio `vswhere` and Windows Kits registration instead of fixed installation paths.

`MsvcKeyboardCompiler` writes the generated four-file source set into a unique `BuildWorkspace`,
runs `cl.exe`, `rc.exe`, and `link.exe` with argument-list process APIs, and returns the deterministic
layout DLL name. `CompilationResult` includes parsed MSVC diagnostics plus the raw log and retained
workspace paths.

The default `KeepFailedBuild` cleanup policy removes `generated/` and `obj/` after success while
retaining `output/` and `logs/`. Failures and cancellations keep the complete workspace for
troubleshooting. `DeleteFailedBuild` removes failed/cancelled workspaces; `KeepAll` also preserves
successful intermediates.
