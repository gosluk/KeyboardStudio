# .NET 10 SDK is copied into the non-root runner user's home directory.
# This makes actions/setup-dotnet able to use/update the installation without root.
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS dotnet-sdk

FROM ubuntu:24.04 AS xkbcommon-builder

ARG XKB_COMMON_VERSION=1.13.1
ARG XKB_COMMON_SHA256=aeb951964c2f7ecc08174cb5517962d157595e9e3f38fc4a130b91dc2f9fec18

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       bison \
       build-essential \
       ca-certificates \
       curl \
       flex \
       libxml2-dev \
       meson \
       ninja-build \
       pkg-config \
       python3 \
       xkb-data \
    && curl --fail --show-error --location \
       --output /tmp/libxkbcommon.tar.gz \
       "https://github.com/xkbcommon/libxkbcommon/archive/refs/tags/xkbcommon-${XKB_COMMON_VERSION}.tar.gz" \
    && echo "${XKB_COMMON_SHA256}  /tmp/libxkbcommon.tar.gz" | sha256sum --check - \
    && mkdir -p /tmp/libxkbcommon \
    && tar --extract --gzip --file /tmp/libxkbcommon.tar.gz \
       --directory /tmp/libxkbcommon --strip-components=1 \
    && meson setup /tmp/libxkbcommon/build /tmp/libxkbcommon \
       --buildtype=release \
       --prefix=/usr/local \
       -Denable-bash-completion=false \
       -Denable-docs=false \
       -Denable-tools=true \
       -Denable-wayland=false \
       -Denable-x11=false \
       -Denable-xkbregistry=true \
       -Dxkb-config-root=/usr/share/X11/xkb \
    && meson compile -C /tmp/libxkbcommon/build \
    && DESTDIR=/xkbcommon-root meson install -C /tmp/libxkbcommon/build

FROM ubuntu:24.04

ARG RUNNER_VERSION=2.335.1
ARG RUNNER_ARCH=x64
ARG RUNNER_SHA256=4ef2f25285f0ae4477f1fe1e346db76d2f3ebf03824e2ddd1973a2819bf6c8cf

ENV DEBIAN_FRONTEND=noninteractive \
    RUNNER_HOME=/home/runner/actions-runner \
    RUNNER_WORKDIR=_work \
    REPO_URL=https://github.com/gosluk/KeyboardStudio \
    DOTNET_ROOT=/home/runner/.dotnet \
    DOTNET_INSTALL_DIR=/home/runner/.dotnet \
    DOTNET_CLI_HOME=/home/runner \
    NUGET_PACKAGES=/home/runner/.nuget/packages \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    PATH=/home/runner/.dotnet:/home/runner/.dotnet/tools:${PATH}

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       bash \
       build-essential \
       buildah \
       ca-certificates \
       clang \
       cmake \
       curl \
       fuse-overlayfs \
       git \
       git-lfs \
       jq \
       libicu74 \
       libicu-dev \
       libssl3 \
       libssl-dev \
       libxml2 \
       openssh-client \
       pkg-config \
       python3 \
       slirp4netns \
       tar \
       uidmap \
       unzip \
       zip \
       zlib1g \
       zlib1g-dev \
       xkb-data \
       xvfb \
       libx11-6 \
       libxrandr2 \
       libxi6 \
       libxcursor1 \
       libxext6 \
       libxrender1 \
       libice6 \
       libsm6 \
       libfontconfig1 \
       libgl1 \
       libegl1 \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 runner \
    && useradd \
       --create-home \
       --uid 10001 \
       --gid 10001 \
       --shell /bin/bash \
       runner \
    # Subordinate UID/GID range for rootless buildah's user-namespace builds
    # (RUN steps in a Containerfile run as fake-root inside this range).
    && echo "runner:100000:65536" >> /etc/subuid \
    && echo "runner:100000:65536" >> /etc/subgid \
    && mkdir -p \
       "${RUNNER_HOME}" \
       /home/runner/.dotnet \
       /home/runner/.dotnet/tools \
       /home/runner/.nuget/packages \
       /home/runner/.cache \
       /home/runner/_tool \
    && chown -R runner:runner /home/runner

# Ubuntu 24.04 packages xkbcli 1.6, which cannot resolve the layered user roots
# KeyboardStudio emits. Keep the verified build separate from distro-owned XKB data.
ARG XKB_COMMON_VERSION=1.13.1
COPY --from=xkbcommon-builder /xkbcommon-root/usr/local/ /usr/local/

RUN ldconfig \
    && test "$(xkbcli --version)" = "${XKB_COMMON_VERSION}" \
    && xkbcli list --help >/dev/null

# Keep the SDK inside a runner-owned location. setup-dotnet defaults to a
# root-owned system directory on Linux unless DOTNET_INSTALL_DIR is provided.
COPY --from=dotnet-sdk --chown=10001:10001 /usr/share/dotnet /home/runner/.dotnet

RUN ln -s /home/runner/.dotnet/dotnet /usr/local/bin/dotnet \
    && dotnet --info \
    && dotnet --list-sdks | grep -E '^10\.'

WORKDIR ${RUNNER_HOME}

RUN curl --fail --show-error --location \
      --output actions-runner.tar.gz \
      "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/actions-runner-linux-${RUNNER_ARCH}-${RUNNER_VERSION}.tar.gz" \
    && echo "${RUNNER_SHA256}  actions-runner.tar.gz" | sha256sum --check - \
    && tar --extract --gzip --file actions-runner.tar.gz \
    && rm actions-runner.tar.gz \
    && ./bin/installdependencies.sh \
    && chown -R runner:runner /home/runner

COPY --chown=runner:runner entrypoint.sh /entrypoint.sh
RUN chmod 0755 /entrypoint.sh

USER root

RUN mkdir -p \
        /home/runner/actions-runner/_work/_temp \
        /home/runner/actions-runner/_work/_tool \
    && chown -R 10001:10001 \
        /home/runner/actions-runner \
        /home/runner/.dotnet \
        /home/runner/.nuget \
        /home/runner/.cache \
    && chmod -R u+rwX /home/runner/actions-runner

USER runner

ENTRYPOINT ["/entrypoint.sh"]
