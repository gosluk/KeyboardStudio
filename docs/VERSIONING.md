# KeyboardStudio versioning

KeyboardStudio has three independent version tracks:

| Track | Owner | Current value | Change rule |
| --- | --- | ---: | --- |
| Application | `KeyboardStudio.App.csproj` `Version` | `0.1.0` | User-visible desktop release |
| Project document | `JsonKeyboardProjectDocumentStore.CurrentDocumentSchemaVersion` | `2` | Outer envelope/profile contract changes |
| Core project | `KeyboardProjectSchema.CurrentVersion` | `1` | Platform-neutral project JSON changes |

The document schema moved to `2` when the envelope gained `importProvenance`, while the release and
the Core project schema stayed where they were. That the three numbers disagree is the point of
having three of them.

An application release does not imply a schema change. Increment a schema only when persisted JSON
requires a new contract, and provide the corresponding compatibility or migration path. The
user-managed `project.metadata.version` is a fourth value owned by the layout author and is not a
KeyboardStudio release identifier.

The application assembly and self-contained package use semantic version `0.1.0`. Run either the
source launcher or a published Linux app host with `--version` to inspect it:

```bash
./scripts/run-app.sh --version
./artifacts/KeyboardStudio-0.1.0-linux-x64/KeyboardStudio --version
```

Release archives include application version and runtime identifier:

```text
KeyboardStudio-0.1.0-linux-x64.tar.gz
KeyboardStudio-0.1.0-win-x64.zip
```

Before changing the application version, update release notes and validate both native packages.
Before changing either schema, update `PROJECT-FORMAT.md`, migration tests, and error handling for
older and future files.
