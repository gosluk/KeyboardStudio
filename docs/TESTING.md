# KeyboardStudio Testing Conventions

This document defines the baseline testing conventions for KeyboardStudio. The detailed implementation roadmap remains in [`IMPLEMENTATION-PLAN.md`](IMPLEMENTATION-PLAN.md).

## Local Podman validation

Run the same restore, Release build, and fast platform-neutral test sequence in an isolated .NET SDK container:

```bash
./scripts/test-in-podman.sh
```

The script uses the .NET `10.0.302` SDK image pinned by `global.json` and pulls it when missing. Set `KEYBOARDSTUDIO_DOTNET_IMAGE` to override the image for compatibility testing. Build outputs are written to the normal ignored `bin/` and `obj/` directories in the checkout.

## Continuous integration runner preference

The main managed GitHub Actions build targets runners labeled `self-hosted`, `Linux`, and `X64`.
Using the generic platform labels lets any matching repository runner accept the job while keeping
the workflow independent of a machine-specific label such as `cherry-home-runner-1`.

GitHub Actions does not provide an ordered `runs-on` fallback from self-hosted to hosted runners.
If no matching self-hosted runner is online, the job intentionally remains queued instead of
silently consuming a GitHub-hosted runner. XKB integration is intentionally a separate
`ubuntu-latest` job because the external verifier needs packages that the unprivileged self-hosted
runner cannot install; this is verification coverage, not a fallback for the managed build.

Windows native integration runs independently on `windows-latest`. The job proves that Visual Studio
contains the MSVC x64 tools and that a Windows 10/11 SDK is registered before it restores and builds
the complete solution. It then runs the platform-neutral suites and the categorized native test,
which compiles generated source, verifies the DLL structure and export, and performs a load-level
smoke test. A missing Windows toolchain is a CI failure, never a silent native-test skip.
Failed Windows native workspaces remain under `TestResults/windows-integration` and are uploaded for
seven days. They contain generated C and headers, per-tool compiler/resource/linker logs, the
combined build log, and `native-build-diagnostics.json`. Successful workspaces are deleted by the
test and are never uploaded.

## Test categories

Every test has exactly one `Category` trait. CI and local scripts select categories explicitly:

| Category | Purpose | Runner |
| --- | --- | --- |
| `Unit` | Fast tests with no native tool dependency | Linux and Windows |
| `Golden` | Deterministic source/reference comparisons | Linux and Windows |
| `XkbIntegration` | Generated XKB compilation with `xkbcli` | Ubuntu with XKB packages |
| `WindowsIntegration` | Generated DLL compilation and verification with MSVC | Windows with Visual Studio and Windows SDK |

The platform-neutral gate is `Category=Unit|Category=Golden`. Native categories are invoked in
dedicated steps so missing tools cannot turn into an accidental fast-test pass.

## Test method naming

Behavior tests use the following form:

```text
Subject_WhenCondition_ExpectedResult
```

`Subject` should normally be the public method or operation under test. When one test intentionally spans two operations, a concise behavior name such as `SaveAndLoad` is acceptable.

Examples:

```text
MapCharacter_WhenMappingExists_ReplacesOutput
LoadAsync_WhenSchemaVersionUnknown_RejectsProject
Translate_WhenAltGrCharacterExists_MapsExpectedModifierState
Generate_WhenInputIsIdentical_ProducesIdenticalSource
```

Names should describe observable behavior rather than implementation details. Avoid generic names such as `Works`, `Test1`, `Success`, or names that only repeat the class name.

## Test structure

Prefer a visible Arrange / Act / Assert flow without mandatory region comments:

1. arrange only the state relevant to the behavior;
2. perform one logical action;
3. assert the externally observable result.

A test may contain several assertions when they describe one result. Split the test when failures would represent independent behaviors.

## Determinism

Tests for persistence, translation, and source generation should avoid dependence on:

- current culture;
- current time unless explicitly controlled;
- machine-specific paths;
- collection ordering that is not part of the contract;
- operating-system behavior in platform-neutral test suites.

Windows-native integration tests use the `WindowsIntegration` category. They detect the toolchain and
return without invoking native tools when it is unavailable, allowing the same suite to run on Linux.
A configured Windows runner exercises the real generated-source-to-DLL path.

PE structure and export parsing use synthetic fixtures and run on every host. The load-level smoke
test is reported as not run unless the test process is Windows and matches the artifact architecture.
Reproducibility unit tests compare source dictionaries and binary hashes without MSVC; the native
integration path can enable `BuildOptions.VerifyReproducibility` on a configured Windows runner.

Linux XKB integration tests use the `XkbIntegration` category. A dedicated GitHub-hosted Ubuntu job
installs `xkbcli` and `xkeyboard-config`, then compiles an ISO AltGr/Unicode fixture and an ANSI
two-level fixture in isolated roots. This keeps package installation off the unprivileged self-hosted
runner. The tests never activate a layout. On failure, the generated symbols component and
`xkbcli.log` remain under `TestResults/xkb-integration` and are uploaded as a workflow artifact.
Locally, categorized tests return without running when `xkbcli` is unavailable; install the tool or
run `scripts/test-xkb-integration-in-podman.sh` to exercise the external verifier in isolation.

## Test project boundaries

- `KeyboardStudio.Core.Tests` covers platform-neutral domain, editing, validation, and persistence-facing behavior.
- `KeyboardStudio.Windows.Tests` covers Windows translation and source generation without requiring native compilation unless explicitly categorized.
- `KeyboardStudio.Linux.Tests` covers physical key-name mapping, keysym/level translation,
  deterministic symbols generation, manifests, verifier behavior, golden files, and categorized
  `xkbcli` integration.
- Native toolchain and artifact tests must not make the Ubuntu platform-neutral test gate Windows-dependent.

## Analyzer exception for test names

The repository intentionally permits underscores in test method names because the separators make the behavior contract readable. `.editorconfig` disables `CA1707` only for `tests/**/*.cs`; production source continues to use the normal analyzer policy.

## Regression rule

A bug fix should add a regression test when the failure can be reproduced deterministically at the relevant subsystem boundary. The regression test name should state the failing condition and expected behavior rather than reference an issue number alone.
