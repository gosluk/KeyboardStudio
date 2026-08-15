#!/usr/bin/env bash

set -euo pipefail

script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_directory}/.." && pwd)"
dotnet_image="${KEYBOARDSTUDIO_DOTNET_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0.302}"

if ! command -v podman >/dev/null 2>&1; then
    echo "Podman is required for isolated XKB integration tests but was not found in PATH." >&2
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
        ./scripts/install-xkbcli.sh
        dotnet restore tests/KeyboardStudio.Linux.Tests/KeyboardStudio.Linux.Tests.csproj
        dotnet test tests/KeyboardStudio.Linux.Tests/KeyboardStudio.Linux.Tests.csproj \
            --configuration Release \
            --verbosity normal \
            --filter "Category=XkbIntegration"
    '
