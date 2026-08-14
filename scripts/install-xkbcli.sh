#!/usr/bin/env bash

set -euo pipefail

if command -v xkbcli >/dev/null 2>&1; then
    xkbcli --version
    exit 0
fi

run_privileged() {
    if [ "$(id -u)" -eq 0 ]; then
        "$@"
    else
        sudo "$@"
    fi
}

if command -v apt-get >/dev/null 2>&1; then
    run_privileged apt-get update
    run_privileged apt-get install -y libxkbcommon-tools xkb-data
elif command -v dnf >/dev/null 2>&1; then
    run_privileged dnf install -y libxkbcommon-utils xkeyboard-config
elif command -v zypper >/dev/null 2>&1; then
    run_privileged zypper --non-interactive install libxkbcommon-tools xkeyboard-config
elif command -v pacman >/dev/null 2>&1; then
    run_privileged pacman --sync --refresh --noconfirm libxkbcommon xkeyboard-config
else
    echo "No supported package manager was found. Install xkbcli and xkeyboard-config manually." >&2
    exit 1
fi

xkbcli --version
