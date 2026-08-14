# KeyboardStudio.Build

Build orchestration and native toolchain integration.

Responsibilities:

- detect whether the Windows native build environment is available;
- invoke project validation before build;
- coordinate artifact generation;
- invoke the native compiler/linker through `INativeCompiler`;
- return structured compiler diagnostics and artifact paths.

The build pipeline must keep source generation testable without requiring the native toolchain.
