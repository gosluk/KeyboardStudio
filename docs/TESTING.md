# KeyboardStudio Testing Conventions

This document defines the baseline testing conventions for KeyboardStudio. The detailed implementation roadmap remains in [`IMPLEMENTATION-PLAN.md`](IMPLEMENTATION-PLAN.md).

## Local Podman validation

Run the same restore, Release build, and complete solution test sequence in an isolated .NET SDK container:

```bash
./scripts/test-in-podman.sh
```

The script uses the .NET `10.0.302` SDK image pinned by `global.json` and pulls it when missing. Set `KEYBOARDSTUDIO_DOTNET_IMAGE` to override the image for compatibility testing. Build outputs are written to the normal ignored `bin/` and `obj/` directories in the checkout.

## Continuous integration runner preference

The managed GitHub Actions build targets runners labeled `self-hosted`, `Linux`, and `X64`. Using
the generic platform labels lets any matching repository runner accept the job while keeping the
workflow independent of a machine-specific label such as `cherry-home-runner-1`.

GitHub Actions does not provide an ordered `runs-on` fallback from self-hosted to hosted runners.
If no matching self-hosted runner is online, the job intentionally remains queued instead of
silently consuming a GitHub-hosted runner. A separately named hosted fallback job can be added later
if that tradeoff becomes desirable.

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

Windows-native integration tests will be isolated and categorized when Phase 10 introduces them.

## Test project boundaries

- `KeyboardStudio.Core.Tests` covers platform-neutral domain, editing, validation, and persistence-facing behavior.
- `KeyboardStudio.Windows.Tests` covers Windows translation and source generation without requiring native compilation unless explicitly categorized later.
- Native toolchain and artifact tests must not make the Ubuntu platform-neutral test gate Windows-dependent.

## Analyzer exception for test names

The repository intentionally permits underscores in test method names because the separators make the behavior contract readable. `.editorconfig` disables `CA1707` only for `tests/**/*.cs`; production source continues to use the normal analyzer policy.

## Regression rule

A bug fix should add a regression test when the failure can be reproduced deterministically at the relevant subsystem boundary. The regression test name should state the failing condition and expected behavior rather than reference an issue number alone.
