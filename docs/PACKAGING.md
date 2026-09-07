# KeyboardStudio desktop packaging

KeyboardStudio publishes a self-contained x64 desktop directory:

| Runtime identifier | Host | Archive |
| --- | --- | --- |
| `linux-x64` | x64 Linux desktop | tar.gz |

The GitHub Actions build publishes it and uploads the archive as a workflow artifact. Self-contained
output includes the .NET runtime; end users do not need the .NET SDK or a system .NET installation.

The application package contains the managed XKB translator, generator, and verifier. It can
always write and structurally validate an XKB symbols component without a compiler or development
package. If `xkbcli` is installed on the user's machine, KeyboardStudio also invokes it for optional
external verification. Its absence does not disable generation.

## Local publish

Use the repository-pinned SDK:

```bash
dotnet restore src/KeyboardStudio.App/KeyboardStudio.App.csproj --runtime linux-x64
dotnet publish src/KeyboardStudio.App/KeyboardStudio.App.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --no-restore \
  --output artifacts/KeyboardStudio-0.1.0-linux-x64
```

Release archives include the application version; see [`VERSIONING.md`](VERSIONING.md). Publish on a
Linux desktop before a release so the packaged app can be smoke-tested in its native environment. The
`artifacts/` directory is ignored and may be deleted after inspection.

## Build and start from source

On Linux, macOS, WSL with GUI support, or another Bash environment, run:

```bash
./scripts/run-app.sh
```

The launcher restores the solution, compiles the Avalonia application, and starts the compiled app.
The repository's `global.json` accepts compatible .NET 10 feature bands, so the launcher does not
require one exact SDK patch version. Set `KEYBOARDSTUDIO_CONFIGURATION=Release` for a Release build.
Additional arguments are forwarded to KeyboardStudio.

## Release checks

Before publishing archives:

- restore and publish with the SDK pinned by `global.json`;
- ensure the archive contains the native app host, managed assemblies, Avalonia assets, and runtime;
- start the package on a Linux desktop;
- generate an XKB artifact on a machine without `xkbcli` and confirm the result is marked
  unverified rather than failed;
- never bundle generated user layouts, build workspaces, signing keys, or development toolchains.
