# KeyboardStudio desktop packaging

KeyboardStudio publishes self-contained x64 desktop directories for two runtime identifiers:

| Runtime identifier | Host | Archive |
| --- | --- | --- |
| `win-x64` | Windows 10/11 x64 | ZIP |
| `linux-x64` | x64 Linux desktop | tar.gz |

The GitHub Actions build publishes each runtime on its matching hosted operating system and uploads
the archive as a workflow artifact. Self-contained output includes the .NET runtime; end users do
not need the .NET SDK or a system .NET installation.

The Linux application package contains the managed XKB translator, generator, and verifier. It can
always write and structurally validate an XKB symbols component without a compiler or development
package. If `xkbcli` is installed on the user's machine, KeyboardStudio also invokes it for optional
external verification. Its absence does not disable generation.

## Local publish

Use the repository-pinned SDK and choose one supported runtime:

```bash
dotnet restore src/KeyboardStudio.App/KeyboardStudio.App.csproj --runtime linux-x64
dotnet publish src/KeyboardStudio.App/KeyboardStudio.App.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --no-restore \
  --output artifacts/KeyboardStudio-0.1.0-linux-x64
```

Replace `linux-x64` with `win-x64` for Windows. Release archives include the application version;
see [`VERSIONING.md`](VERSIONING.md). Publish on the target operating system before a
release so the packaged app can be smoke-tested in its native desktop environment. The `artifacts/`
directory is ignored and may be deleted after inspection.

## Build and start from source

On Linux, macOS, WSL with GUI support, or another Bash environment, run:

```bash
./scripts/run-app.sh
```

The launcher restores the solution, compiles the Avalonia application, and starts the compiled app.
Set `KEYBOARDSTUDIO_CONFIGURATION=Release` for a Release build. Additional arguments are forwarded
to KeyboardStudio.

## Release checks

Before publishing archives:

- restore and publish with the SDK pinned by `global.json`;
- ensure the archive contains the native app host, managed assemblies, Avalonia assets, and runtime;
- start the package on its target OS;
- on Linux, generate an XKB artifact on a machine without `xkbcli` and confirm the result is marked
  unverified rather than failed;
- never bundle generated user layouts, build workspaces, signing keys, or development toolchains.
