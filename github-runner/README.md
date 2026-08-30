# KeyboardStudio self-hosted GitHub runner

This image builds a self-hosted GitHub Actions runner for the KeyboardStudio CI pool
(`self-hosted`, `Linux`, `X64` — see [`.github/workflows/build.yml`](../.github/workflows/build.yml)
and [`docs/TESTING.md`](../docs/TESTING.md)). These runners are dedicated to
`github.com/gosluk/KeyboardStudio`; `start-runners.sh` registers against it directly rather than
prompting for a target.

## Security first

- **Never commit a registration token.** `RUNNER_TOKEN` is always supplied at deploy time via
  an environment variable (or a Kubernetes Secret for `k8s-runner.yaml`) and is never baked into
  an image or a compose file. Registration tokens expire after about an hour.
- The runner user is unprivileged (uid/gid `10001`, no `sudo`) and cannot install packages or
  escalate at runtime — anything a workflow job needs must be baked into this image at build
  time instead. The image builds checksum-pinned `libxkbcommon` 1.13.1 because Ubuntu 24.04's
  1.6 package cannot verify KeyboardStudio's layered per-user XKB roots. See `docs/TESTING.md`
  for the remaining package list.
- This image does not expose a Docker/Podman socket to workflow jobs. `buildah`,
  `fuse-overlayfs`, `slirp4netns`, and a subordinate UID/GID range are baked in instead, so a
  job that genuinely needs to build or run a container can do so rootless, without nesting a
  privileged container inside the runner's own container.

## Build

```bash
podman build \
  --build-arg RUNNER_VERSION=2.335.1 \
  --build-arg RUNNER_SHA256=4ef2f25285f0ae4477f1fe1e346db76d2f3ebf03824e2ddd1973a2819bf6c8cf \
  -t localhost/keyboardstudio-runner:latest .
```

The build checks the libxkbcommon source archive checksum and fails unless the installed
`xkbcli --version` is exactly `1.13.1`. Change both XKB build arguments deliberately when
upgrading it.

## Run locally

```bash
podman run --rm \
  -e REPO_URL='https://github.com/gosluk/KeyboardStudio' \
  -e RUNNER_TOKEN='NEW_TOKEN' \
  -e RUNNER_NAME='keyboardstudio-runner-1' \
  -e RUNNER_LABELS='self-hosted,Linux,X64,keyboardstudio-runner-1' \
  localhost/keyboardstudio-runner:latest
```

## Run multiple runners via docker-compose / podman-compose

`docker-compose.yml` is the static 2-runner default (`keyboardstudio-runner-1`/`-2`),
sharing one registration token but with isolated `_work` volumes. `REPO_URL` and
`RUNNER_TOKEN` are required env vars (no default baked in).

For customized runs — a different name prefix, runner count, or RAM limit — use
`start-runners.sh`, which generates `docker-compose.generated.<prefix>.yml` (gitignored —
it is a deploy-time artifact, not something to commit) from these settings and registers
against `gosluk/KeyboardStudio`:

```bash
./start-runners.sh [-p|--prefix NAME] [-n|--count N] [-r|--ram SIZE] [--skip-build]
```

- `--prefix` — name prefix for runner services/containers, e.g. `myrunner` produces
  `myrunner-1`, `myrunner-2`, ... (default: `keyboardstudio-runner`). It also names the
  generated compose file (`docker-compose.generated.myrunner.yml`) and the compose
  project (`myrunner-compose`), so different prefixes can run side by side without
  colliding.
- `--count` — number of runners to start (default: `2`)
- `--ram` — memory limit per runner, e.g. `4gb`, `4096m` (default: `8gb`)
- `--skip-build` — explicitly reuse the existing local image. Without it, the script rebuilds
  `localhost/keyboardstudio-runner:latest` before starting the stack so Dockerfile dependency
  updates are not missed; unchanged layers remain cached.
- CPU limit is not configurable — each runner is always given a limit equal to
  the host's full core count (`nproc`), so runners can use all available CPUs.

Any of `--prefix`/`--count`/`--ram` you don't pass as a flag is prompted for
interactively instead (press Enter to accept the default shown in brackets).
The script then prompts for the registration token (input hidden, or fetched
automatically via `gh api` if you're already authenticated), then runs the
generated compose stack detached (`podman-compose` if available, else
`docker compose`).

## Deploy to k3s (optional)

`k8s-runner.yaml` is an alternative to the compose-based deployment above, for running a
runner as a Kubernetes Deployment instead. Create the token Secret out-of-band (see the
comment at the top of the file), then:

```bash
kubectl create namespace github-runners
kubectl apply -f k8s-runner.yaml
```

For multiple runners, duplicate the Deployment block, changing the Secret name, Deployment
name, `app` labels, and token value for each one.
