# KeyboardStudio Testing Conventions

This document defines the baseline testing conventions for KeyboardStudio. The detailed implementation roadmap remains in [`IMPLEMENTATION-PLAN.md`](IMPLEMENTATION-PLAN.md).

## Local Podman validation

Run the same restore, Release build, and fast platform-neutral test sequence in an isolated .NET SDK container:

```bash
./scripts/test-in-podman.sh
```

The script uses the .NET `10.0.302` SDK image pinned by `global.json` and pulls it when missing. Set `KEYBOARDSTUDIO_DOTNET_IMAGE` to override the image for compatibility testing. Build outputs are written to the normal ignored `bin/` and `obj/` directories in the checkout.

Two more scripts run the jobs whose tooling a developer machine is not expected to supply, without
installing anything on it. CI runs these jobs directly rather than through these scripts, because
its runners are already containers:

```bash
./scripts/test-xkb-integration-in-podman.sh   # xkbcli and xkeyboard-config
./scripts/package-linux-in-podman.sh          # Xvfb and the libraries Avalonia binds
```

## Continuous integration runner preference

The main managed GitHub Actions build targets runners labeled `self-hosted`, `Linux`, and `X64`.
Using the generic platform labels lets any matching repository runner accept the job while keeping
the workflow independent of a machine-specific label such as `cherry-home-runner-1`.

GitHub Actions does not provide an ordered `runs-on` fallback from self-hosted to hosted runners.
If no matching self-hosted runner is online, the job intentionally remains queued instead of
silently consuming a GitHub-hosted runner.

The managed build and the release gate run on that pool. Two Linux jobs need software beyond the
SDK and do not: XKB integration needs `xkbcli` and `xkeyboard-config`, and packaging needs a display
server and the libraries Avalonia binds. The runners are unprivileged podman containers — uid 10001,
no `sudo`, `apt-get` present but unusable — so they can install nothing at runtime, and the two jobs
run on `ubuntu-latest` instead.

Running them in a container on the pool was considered and rejected: that nests a container inside
the runner, which is already one, needing privileged mode and nested user namespaces to buy an
isolation boundary that already exists.

**Moving them onto the pool** takes one change to the runner image and one line per job. Add to the
image (the base is Debian/Ubuntu):

```
libxkbcommon-tools xkb-data                                    # XKB integration
xvfb libx11-6 libxrandr2 libxi6 libxcursor1 libxext6     libxrender1 libice6 libsm6 libfontconfig1 libgl1 libegl1   # packaging
```

Then change each job's `runs-on: ubuntu-latest` to `runs-on: [self-hosted, Linux, X64]` and drop
its package-installation step. The `scripts/install-xkbcli.sh` and `scripts/install-xvfb.sh` helpers
stay useful for a developer machine, where sudo does exist.

Windows integration and packaging share one `windows-latest` job, the only one that cannot move to
the pool: it needs MSVC, the Windows SDK, and a Windows loader. Running them together stops the two
repeating the same checkout, SDK setup and restore on separate runners, and means an artifact is
only ever produced from a tree whose tests have passed. The job proves that Visual Studio contains
the MSVC x64 tools and that a Windows 10/11 SDK is registered before it restores and builds the
complete solution. It then runs the platform-neutral suites and the categorized native test, which
compiles generated source, verifies the DLL structure and export, and performs a load-level smoke
test. A missing Windows toolchain is a CI failure, never a silent native-test skip.

The platform-neutral suites run on both the self-hosted pool and the Windows job, which is not
duplication: it is the only check that they are platform-neutral at all. It matters most for the
XKB backend, because the shipped product offers the Linux target on every host it runs on, so a
Windows user authoring an XKB layout is exercising it. Test doubles are held to the same standard —
`FakeXkbFileSystem` models a POSIX filesystem on every host rather than deferring to `Path`, whose
separator would otherwise make every import test Linux-only.
Failed Windows native workspaces remain under `TestResults/windows-integration` and are uploaded for
seven days. They contain generated C and headers, per-tool compiler/resource/linker logs, the
combined build log, and `native-build-diagnostics.json`. Successful workspaces are deleted by the
test and are never uploaded.

The native matrix compiles four project-level fixtures: simple ANSI US-like letters, an ANSI AltGr
Unicode mapping, an ISO-105 layout using the extra ISO key, and a special-key layout containing both
ordinary and extended scan codes. Every fixture must produce a structurally valid x64 DLL exporting
`KbdLayerDescriptor`, pass the matching-host load check, and reproduce byte-for-byte.

## Test categories and facets

Every test has one primary `Category` trait. A test may also have a secondary `ErrorPath` category
facet so the release failure matrix can be run independently. CI and local scripts select categories
explicitly:

| Category | Purpose | Runner |
| --- | --- | --- |
| `Unit` | Fast tests with no native tool dependency | Linux and Windows |
| `Golden` | Deterministic source/reference comparisons | Linux and Windows |
| `XkbIntegration` | Generated XKB compilation with `xkbcli` | Ubuntu with XKB packages |
| `WindowsIntegration` | Generated DLL compilation and verification with MSVC | Windows with Visual Studio and Windows SDK |
| `ErrorPath` | Cross-project release failure-path facet; also retains its primary category | Any runner required by the primary category |

The platform-neutral gate is `Category=Unit|Category=Golden`. Native categories are invoked in
dedicated steps so missing tools cannot turn into an accidental fast-test pass.

Run the MVP error-path matrix directly with:

```bash
dotnet test KeyboardStudio.slnx --filter "Category=ErrorPath"
```

The matrix covers invalid and future-schema project documents, missing target profiles, unsupported
target mappings, absent Windows tooling, compiler failures, missing/rejecting XKB verification,
unwritable output, and cancellation.

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

Linux XKB integration tests use the `XkbIntegration` category. A dedicated Ubuntu job installs
`xkbcli` and `xkeyboard-config`, then compiles an ISO AltGr/Unicode fixture and an ANSI two-level
fixture in isolated roots. The tests never activate a layout. On failure, the generated symbols component and
`xkbcli.log` remain under `TestResults/xkb-integration` and are uploaded as a workflow artifact.
Locally, categorized tests return without running when `xkbcli` is unavailable; install the tool or
run `scripts/test-xkb-integration-in-podman.sh` to exercise the external verifier in isolation.

The same category also covers layout import against the installed database, in both
`KeyboardStudio.Linux.Tests` and `KeyboardStudio.App.Tests`. The application half is where the
composition root lives, so it is the only place the real sources and the real host probe can be
shown to be wired together at all — including that the probe and the catalog agree on their
vocabulary well enough for this host's own configured layout to import. Those tests skip on a
developer machine with no XKB database and fail loudly in Linux CI, where the package is installed
deliberately.

The layouts whose imports are asserted exactly come from a pinned copy of xkeyboard-config vendored
into `tests/KeyboardStudio.Linux.Tests/Fixtures/Xkb`, written by `scripts/vendor-xkb-fixtures.py` and
described by the `PROVENANCE.md` beside it. The host's own database answers whether the importer
copes with real data and cannot answer what a particular import produces: it changes under the tests
whenever the distribution updates. So the two questions are asked of two inputs — the corpus soak of
whatever the host ships, the goldens of a copy that never moves.

`XkbGoldenImportTests` snapshots each pinned import in full: the geometry chosen, the name taken, the
four layers of every key, and every diagnostic raised. A failing golden is not automatically a
defect; run the suite with `KEYBOARDSTUDIO_UPDATE_GOLDEN=1` to rewrite the snapshots in the
repository, then read the diff, which is the change under review. That variable is refused when `CI`
is `true`: rewriting is a developer's gesture whose whole effect is to make the suite pass, so
obeying it on a build machine would report success for exactly the changes the goldens exist to
catch. `XkbImportRoundTripTests` composes
the importer with the generator and asserts the pair is lossless over what the model holds:
whatever the first import produced is what importing the generated file returns.

`XkbConformanceOracleTests` is the only test that can grade the composition rules, because it is the
only one that compares against something the project did not write. It flattens a layout with the
resolver, compiles the same layout with `xkbcli compile-keymap`, and requires the two to agree about
every key both name — addressed by physical key rather than by key name, since `keycodes/evdev` gives
most keys two names and a phonetic layout writes both. Keysyms are compared as decoded outputs, so
`U0105` and `aogonek` count as the same answer. It reads the host's database rather than the pinned
fixtures so that both sides are looking at the same bytes and a version difference cannot be mistaken
for a defect.

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
