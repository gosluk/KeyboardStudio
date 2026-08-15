#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "$script_dir/.." && pwd)"
application_project="$repository_root/src/KeyboardStudio.App/KeyboardStudio.App.csproj"
build_configuration="${KEYBOARDSTUDIO_CONFIGURATION:-Debug}"

dotnet restore "$repository_root/KeyboardStudio.slnx"
dotnet build "$application_project" \
  --configuration "$build_configuration" \
  --no-restore

exec dotnet run \
  --project "$application_project" \
  --configuration "$build_configuration" \
  --no-build \
  --no-restore \
  -- "$@"
