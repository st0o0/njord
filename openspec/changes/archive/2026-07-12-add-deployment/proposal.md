## Why

njord is feature-complete (ingest, domain, egress) but has no way to run as a
production service. The Dockerfile exists and the CI/CD pipeline pushes signed
multi-arch images to ghcr.io, yet there is no docker-compose file for the
target environment (Home Assistant host), no HTTP health endpoint for external
monitoring, and the Dockerfile/workflow descriptions still reference the
replaced Kachelmann API. Users need a single `docker compose up -d` to go live
next to their existing Mosquitto broker.

## What Changes

- Switch the host from `Host.CreateApplicationBuilder` to
  `WebApplication.CreateBuilder` to gain Kestrel and the ASP.NET health-check
  infrastructure.
- Add a `GET /healthz` endpoint (always-healthy placeholder) using
  `Microsoft.AspNetCore.Diagnostics.HealthChecks` — extensible for real checks
  (MQTT connected, poll freshness) when the pipeline is revisited.
- Switch the Dockerfile runtime base image from `runtime`-chiseled to
  `aspnet`-chiseled to include the ASP.NET shared framework.
- Add a `docker-compose.yml` that defines only the njord service (Mosquitto is
  external), configured entirely via environment variables, with
  `restart: unless-stopped`.
- Fix outdated "Kachelmann" references in the Dockerfile labels and the
  `release.yml` / `dev-build.yml` GHCR index annotations.
- Verify and, if necessary, adapt the CI smoke test (`ci.yml`) which expects
  `docker run --rm njord:ci` to produce `"njord"` and exit — a WebApplication
  host may change that behavior.

## Non-goals

- Real health checks (MQTT connection status, poll freshness) — deferred to a
  future pipeline redesign where health is an aspect of the full Akka.Streams
  flow.
- Mosquitto or any other service in the compose file — users manage their own
  broker.
- Kubernetes, Swarm, or HA Add-on packaging.
- Docker Compose `healthcheck:` directive — the chiseled image has no
  shell/curl; the HTTP endpoint is consumed by external monitors (Uptime Kuma,
  HA rest sensor).

## Capabilities

### New Capabilities

- `health-endpoint`: Kestrel-hosted HTTP health endpoint (`/healthz`) using
  ASP.NET health-check infrastructure, always-healthy placeholder, extensible
  for future checks.

### Modified Capabilities

- `service-configuration`: The host switches from `GenericHost` to
  `WebApplication` (Kestrel); a health-endpoint port is exposed (default 8080).

## Impact

- **`src/Njord/Njord.csproj`**: SDK changes from `Microsoft.NET.Sdk` to
  `Microsoft.NET.Sdk.Web`.
- **`src/Njord/Program.cs`**: `Host.CreateApplicationBuilder` →
  `WebApplication.CreateBuilder`, add health-check middleware.
- **`Dockerfile`**: base image `runtime` → `aspnet`, labels updated, `EXPOSE`
  for health port.
- **`docker-compose.yml`**: new file at repo root.
- **`.github/workflows/release.yml`**: annotation text updated.
- **`.github/workflows/dev-build.yml`**: annotation text updated.
- **`.github/workflows/ci.yml`**: smoke test may need adjustment.
- **Image size**: ~10 MB larger (`aspnet`-chiseled vs `runtime`-chiseled).
- **No API-budget impact**: no polling changes.
