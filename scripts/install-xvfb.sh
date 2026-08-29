#!/usr/bin/env bash

# Installs the virtual display and the libraries the Avalonia X11 backend binds, which the
# packaged-app startup smoke test needs. Mirrors install-xkbcli.sh: it works as root, escalates
# with sudo when that is available, and otherwise says plainly that neither is, because a smoke
# test that quietly does not run is worse than one that fails.

set -euo pipefail

if command -v xvfb-run >/dev/null 2>&1; then
    echo "xvfb-run is already present."
    exit 0
fi

run_privileged() {
    if [ "$(id -u)" -eq 0 ]; then
        "$@"
    elif command -v sudo >/dev/null 2>&1; then
        sudo "$@"
    else
        echo "Installing Xvfb requires root or sudo. Bake these packages into the runner image, or use scripts/package-linux-in-podman.sh locally." >&2
        exit 1
    fi
}

if command -v apt-get >/dev/null 2>&1; then
    run_privileged apt-get update
    run_privileged apt-get install -y --no-install-recommends \
        xvfb libx11-6 libxrandr2 libxi6 libxcursor1 libxext6 libxrender1 \
        libice6 libsm6 libfontconfig1 libgl1 libegl1
elif command -v dnf >/dev/null 2>&1; then
    run_privileged dnf install -y \
        xorg-x11-server-Xvfb libX11 libXrandr libXi libXcursor libXext libXrender \
        libICE libSM fontconfig mesa-libGL mesa-libEGL
else
    echo "No supported package manager was found. Install Xvfb and the X client libraries manually." >&2
    exit 1
fi

xvfb-run --help >/dev/null 2>&1 && echo "xvfb-run installed."
