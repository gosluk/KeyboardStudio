#!/usr/bin/env bash

set -Eeuo pipefail

: "${REPO_URL:=https://github.com/gosluk/KeyboardStudio}"
: "${RUNNER_NAME:=$(hostname)}"
: "${RUNNER_WORKDIR:=_work}"
: "${RUNNER_LABELS:=self-hosted,Linux,X64}"
: "${RUNNER_EPHEMERAL:=false}"

cd "${RUNNER_HOME:-/home/runner/actions-runner}"

cleanup() {
  echo "Stopping runner..."

  if [[ -f .runner ]]; then
    # Registration tokens are short-lived and are commonly already consumed.
    # Removal is best-effort; generate/provide RUNNER_REMOVE_TOKEN if reliable
    # deregistration is required for non-ephemeral runners.
    if [[ -n "${RUNNER_REMOVE_TOKEN:-}" ]]; then
      ./config.sh remove --unattended --token "${RUNNER_REMOVE_TOKEN}" || true
    fi
  fi
}

trap cleanup EXIT INT TERM

# GitHub Actions Runner creates .runner after successful configuration.
# When the runner directory is persisted in a volume, keep the existing
# configuration instead of trying to configure it again on every restart.
if [[ -f .runner ]]; then
  echo "Runner is already configured; skipping config.sh."
else
  : "${RUNNER_TOKEN:?RUNNER_TOKEN must be provided when configuring a new runner}"

  config_args=(
    --unattended
    --replace
    --url "${REPO_URL}"
    --token "${RUNNER_TOKEN}"
    --name "${RUNNER_NAME}"
    --work "${RUNNER_WORKDIR}"
    --labels "${RUNNER_LABELS}"
  )

  if [[ "${RUNNER_EPHEMERAL,,}" == "true" ]]; then
    config_args+=(--ephemeral)
  fi

  ./config.sh "${config_args[@]}"
fi

exec ./run.sh