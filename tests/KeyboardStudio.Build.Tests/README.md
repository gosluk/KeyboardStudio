# KeyboardStudio.Build.Tests

Covers the target-neutral build pipeline in `KeyboardStudio.Build`: backend resolution, the
validation and cancellation contract `BuildOrchestrator` guarantees every backend, and the shared
external-process runner.

Backend-specific behaviour is tested where the backend lives — the XKB backend in
`KeyboardStudio.Linux.Tests`. Tests here use stub backends so the orchestration contract stays
provable without any host toolchain.

All suites are `Category=Unit`.
