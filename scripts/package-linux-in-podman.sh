#!/usr/bin/env bash

# Publishes the self-contained Linux desktop package and smoke-tests it inside a container.
#
# The packaging job used to run on a GitHub-hosted Ubuntu image for one reason: the smoke test
# needs a display server and the graphics libraries Avalonia binds, and the self-hosted runners
# are unprivileged, so the job could not install them. A container supplies both without asking
# anything of the host beyond podman, which is also what lets a developer reproduce the packaging
# step locally instead of reading the workflow and guessing.

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
dotnet_image="${KEYBOARDSTUDIO_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0.302}"

if ! command -v podman >/dev/null 2>&1; then
    echo "Podman is required for isolated Linux packaging but was not found in PATH." >&2
    exit 1
fi

podman run --rm --pull=missing \
    --volume "${repository_root}:/workspace:Z" \
    --workdir /workspace \
    --env CI=true \
    --env DEBIAN_FRONTEND=noninteractive \
    --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    --env DOTNET_NOLOGO=1 \
    --env NUGET_PACKAGES=/tmp/nuget-packages \
    "${dotnet_image}" \
    sh -eu -c '
        # Xvfb supplies the display; the rest are what the Avalonia X11 backend binds at startup.
        # A missing one of these does not fail the publish, only the smoke test, and it fails there
        # as a bare "Unable to load shared library", so they are installed together and up front.
        apt-get update -qq
        apt-get install -y --no-install-recommends \
            xvfb libx11-6 libxrandr2 libxi6 libxcursor1 libxext6 libxrender1 \
            libice6 libsm6 libfontconfig1 libgl1 libegl1 >/dev/null

        version="$(dotnet msbuild src/KeyboardStudio.App/KeyboardStudio.App.csproj -getProperty:Version)"
        version="$(printf "%s" "${version}" | tr -d "[:space:]")"
        package="KeyboardStudio-${version}-linux-x64"

        dotnet restore src/KeyboardStudio.App/KeyboardStudio.App.csproj --runtime linux-x64
        dotnet publish src/KeyboardStudio.App/KeyboardStudio.App.csproj \
            --configuration Release \
            --runtime linux-x64 \
            --self-contained true \
            --no-restore \
            --output "artifacts/${package}"

        # The packaged executable has to answer without a display before it is asked to raise one,
        # so that a failure here separates "the build is broken" from "the graphics stack is".
        "artifacts/${package}/KeyboardStudio" --version

        set +e
        xvfb-run --auto-servernum timeout 5s "artifacts/${package}/KeyboardStudio"
        exit_code=$?
        set -e

        # 124 is timeout reporting that it had to kill a process still running, which is the only
        # outcome that means the window opened and stayed open. Exiting on its own is a failure
        # however clean the code, because a desktop app that returns immediately never started.
        if [ "${exit_code}" -ne 124 ]; then
            echo "Packaged application exited unexpectedly with code ${exit_code}." >&2
            exit 1
        fi

        tar -C artifacts -czf "artifacts/${package}.tar.gz" "${package}"

        # The caller needs the version to name the uploaded artifact and has no dotnet of its own.
        printf "%s" "${version}" > artifacts/version.txt
    '
