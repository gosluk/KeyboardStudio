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
the workflow independent of a machine-specific label such as `keyboardstudio-runner-1`.

GitHub Actions does not provide an ordered `runs-on` fallback from self-hosted to hosted runners.
If no matching self-hosted runner is online, the job intentionally remains queued instead of
silently consuming a GitHub-hosted runner.

All four Linux jobs — managed build, XKB integration, Linux packaging, and the release gate — run
on that pool. The runners are unprivileged podman containers (`github-runner/`) — uid 10001, no
`sudo`, `apt-get` present but unusable — so they can install nothing at runtime. XKB integration
needs a current `xkbcli` and `xkeyboard-config`; packaging needs a display server and the libraries
Avalonia binds. Both sets of dependencies are baked into the runner image at build time instead
(`github-runner/Dockerfile`, built as root before the image drops to the unprivileged runner user):

```
xkb-data + checksum-pinned libxkbcommon 1.13.1 built under /usr/local                              # XKB integration
xvfb libx11-6 libxrandr2 libxi6 libxcursor1 libxext6 libxrender1 libice6 libsm6 libfontconfig1 libgl1 libegl1   # packaging
```

Ubuntu 24.04's `libxkbcommon-tools` package is version 1.6.0. That version cannot resolve the
layered user and system roots used by generated layouts, and its registry command cannot inspect
multiple roots. The runner therefore builds 1.13.1 from a checksum-pinned source archive while
retaining Ubuntu's `xkb-data` package as the canonical system symbols/rules database. The image
build verifies the resulting version and the presence of `xkbcli list` before it can be deployed.

Each job still calls `scripts/install-xkbcli.sh` / `scripts/install-xvfb.sh` first; both check for
the tool and exit immediately when it is already present, so on the pool they are a no-op and only
do real work on a developer machine that has sudo but lacks the tool.

Running these jobs in a nested container on the pool was considered and rejected: that needs
privileged mode and nested user namespaces to buy an isolation boundary the runner, itself already
a container, already has. The runner image ships `buildah`, `fuse-overlayfs`, `slirp4netns`, and a
subordinate UID/GID range instead, so a job that genuinely needs an ad hoc container can build and
run one rootless without nesting privilege — see `github-runner/README.md`.

Every job in this workflow runs on the self-hosted pool. There is no hosted, quota-limited runner
left in it, so nothing needs a change-detection gate to stay affordable and `mvp-release-gate`
depends on all three build jobs unconditionally: a `skipped` result is a failure, not an excuse.

Test doubles are held to the same standard as production code — `FakeXkbFileSystem` models a POSIX
filesystem explicitly rather than deferring to `Path`, so its behaviour is a stated contract rather
than an accident of the host.

Failed XKB integration artifacts remain under `TestResults/xkb-integration` and are uploaded on
failure. They contain the generated symbols text and the verifier's output. Successful runs retain
nothing.

The XKB matrix compiles project-level fixtures covering ANSI US-like letters, an ANSI AltGr Unicode
mapping, and an ISO-105 layout using the extra ISO key. Every fixture must produce a symbols component
that `xkbcli` accepts and that is byte-for-byte reproducible.

## Test categories and facets

Every test has one primary `Category` trait. A test may also have a secondary `ErrorPath` category
facet so the release failure matrix can be run independently. CI and local scripts select categories
explicitly:

| Category | Purpose | Runner |
| --- | --- | --- |
| `Unit` | Fast tests with no external tool dependency | Any |
| `Golden` | Deterministic source/reference comparisons | Any |
| `XkbIntegration` | Generated XKB compilation with `xkbcli` | Linux with XKB packages |
| `ErrorPath` | Cross-project release failure-path facet; also retains its primary category | Any runner required by the primary category |
| `LinuxHost` | Facet for tests whose subject is a Linux host's own filesystem; also retains its primary category | Any Linux runner |

The tool-independent gate is `Category=Unit|Category=Golden`. Categories that need external tools are
invoked in dedicated steps so a missing tool cannot turn into an accidental fast-test pass.

`LinuxHost` marks tests whose subject is the host's own filesystem — where an XKB database is
installed, and what the XDG base directories resolve to. It is kept as a distinct facet because those
tests assert against a real installed system rather than a fixture, so a failure in them means
something different from a failure in a unit test: the host is not configured as expected, not that
the code is wrong.

Run the MVP error-path matrix directly with:

```bash
dotnet test KeyboardStudio.slnx --filter "Category=ErrorPath"
```

The matrix covers invalid and future-schema project documents, missing target profiles, unsupported
target mappings, missing and rejecting XKB verification, unwritable output, and cancellation.

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

Linux XKB integration tests use the `XkbIntegration` category. A dedicated Ubuntu runner provides
the pinned `xkbcli` and packaged `xkeyboard-config`, then compiles an ISO AltGr/Unicode fixture and an ANSI two-level
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
- `KeyboardStudio.Build.Tests` covers target-neutral orchestration — backend resolution, the
  validation and cancellation contract, and the shared process runner — against stub backends.
- `KeyboardStudio.Linux.Tests` covers physical key-name mapping, keysym/level translation,
  deterministic symbols generation, manifests, verifier behavior, golden files, and categorized
  `xkbcli` integration.
- Tests that need an external tool must not make the tool-independent gate depend on it.

## Analyzer exception for test names

The repository intentionally permits underscores in test method names because the separators make the behavior contract readable. `.editorconfig` disables `CA1707` only for `tests/**/*.cs`; production source continues to use the normal analyzer policy.

## Regression rule

A bug fix should add a regression test when the failure can be reproduced deterministically at the relevant subsystem boundary. The regression test name should state the failing condition and expected behavior rather than reference an issue number alone.
