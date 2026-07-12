## Context

njord runs as a .NET Generic Host (`Host.CreateApplicationBuilder`) with
Akka.NET actors, no HTTP server, no health endpoint. The CI/CD pipeline
already builds multi-arch Docker images, signs them with cosign, and pushes to
`ghcr.io/st0o0/njord` on release-please tags. What's missing is the last mile:
a compose file for the HA host and an HTTP health endpoint for external
monitoring.

The Dockerfile uses `mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled`
(distroless — no shell, no curl). Descriptions in the Dockerfile and workflow
annotations still say "Kachelmann".

The CI smoke test (`ci.yml`) runs `docker run --rm njord:ci` and asserts
stdout equals `"njord"`. With the default `appsettings.json` shipping
`Mqtt.Host: ""`, the options validator fails on startup (`ValidateOnStart`),
so the container crashes immediately. The test likely never passed green with
the current validator. This needs to be investigated and fixed as part of this
change.

## Goals / Non-Goals

**Goals:**

- A single `docker compose up -d` on the HA host runs njord against an
  existing Mosquitto broker, configured entirely through environment variables.
- An HTTP health endpoint (`GET /healthz`) is reachable from external monitors
  (Uptime Kuma, HA rest sensor) and from Docker/orchestrators in the future.
- All public-facing descriptions reflect the current Open-Meteo integration.
- The CI smoke test works correctly with the new host setup.

**Non-Goals:**

- Real health checks (MQTT status, poll freshness) — deferred to a pipeline
  redesign where health is an aspect of the Akka.Streams flow.
- Docker Compose `healthcheck:` directive — chiseled images have no shell/curl
  for an in-container probe. Health monitoring is external.
- Mosquitto or other services in the compose file.
- Kubernetes / Swarm / HA Add-on packaging.

## Decisions

### D1: WebApplication.CreateBuilder replaces Host.CreateApplicationBuilder

**Choice:** Switch to `Microsoft.NET.Sdk.Web` + `WebApplication.CreateBuilder`.

**Alternatives considered:**
- *Manual Kestrel in GenericHost*: avoids SDK change but loses
  `MapHealthChecks`, minimal-API routing, and the ASP.NET shared framework
  conveniences. More code for the same result.
- *Raw TCP health listener*: minimal footprint but no standard health-check
  protocol, no extensibility.

**Rationale:** The Web SDK adds Kestrel, the health-check middleware, and a
path to future endpoints (status JSON, metrics) with zero extra packages.
The `aspnet`-chiseled image is ~10 MB larger than `runtime`-chiseled — acceptable.

### D2: Always-healthy placeholder health check

**Choice:** Register `MapHealthChecks("/healthz")` with no custom
`IHealthCheck` implementations. The endpoint returns `200 Healthy` as long as
the process runs.

**Rationale:** Real checks (MQTT connected, last-poll age) require a state
bridge between actors and DI services. Designing that bridge now, before the
pipeline architecture is revisited, would couple to internals that may change.
The placeholder establishes the infrastructure; checks are added later.

### D3: Kestrel listens on port 8080

**Choice:** Set `ASPNETCORE_URLS=http://+:8080` in the Dockerfile as a default.
Users can override via environment variable.

**Rationale:** 8080 is the conventional non-root HTTP port. The chiseled image
runs as non-root, so port 80 is not available without capabilities. 8080 avoids
conflicts with common services on the HA host.

### D4: Compose file at repo root, njord-only

**Choice:** A `docker-compose.yml` at the repo root defining a single `njord`
service. Image from `ghcr.io/st0o0/njord:latest`. Configuration via
`environment:` keys using the .NET `__` separator convention. No Mosquitto, no
volumes, no networks beyond the default.

**Rationale:** The user's Mosquitto broker is already running (HA Add-on or
separate container). Bundling it would force opinions about auth, TLS, and
persistence that vary per setup. The compose file is a deployment reference,
not a full stack.

### D5: Smoke test strategy

**Choice:** Investigate the current smoke test. It asserts
`docker run --rm njord:ci` outputs `"njord"` — but with `Mqtt.Host: ""`
the validator crashes the host on startup. Options:
1. Add a `--version` flag that prints the assembly version and exits before
   host startup.
2. Pass a valid dummy config via env vars so the host starts, then check for a
   health response with a timeout.
3. Simply verify the container starts and the health endpoint responds (run
   with detach, curl health, stop).

Option 1 is simplest and decouples the smoke test from runtime config. But
with the chiseled image having no curl, option 3 needs `docker exec` which
also requires a shell. Option 1 (version flag) is the cleanest.

**Decision:** The smoke test approach will be determined during implementation
by investigating why the current test expects `"njord"` output and adapting
accordingly.

## Risks / Trade-offs

- **[Image size +10 MB]** → Acceptable for the extensibility gained. The
  `aspnet`-chiseled image is still under 50 MB.
- **[Kestrel attack surface]** → Kestrel listens only on 8080, serves only
  `/healthz`. No auth, no static files, no controllers. The compose file
  does not expose the port to the host by default — users opt in.
- **[Smoke test breakage]** → The current test may already be broken. Will be
  investigated and fixed as part of this change.

## Open Questions

- What is the intended behavior of the current CI smoke test (`"njord"`
  output)? Needs investigation during implementation.
