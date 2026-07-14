## Context

njord ships as a Docker container that connects to an external Mosquitto broker (typically on the Home Assistant host). The current `docker-compose.yml` shows only the minimum required options and assumes the broker already exists. There is no way to run njord in isolation for development or evaluation.

Aspire 13 (stable at 13.4.6) provides container orchestration, dashboard, and launch profiles — a natural fit for a "one-click dev environment" that bundles all dependencies.

## Goals / Non-Goals

**Goals:**
- Provide a fully-commented `docker-compose.example.yml` as the configuration reference for all njord options.
- Provide an Aspire AppHost that starts njord + Mosquitto + MQTT Explorer with a single F5.
- Support both SQLite (default) and PostgreSQL persistence via launch profiles.
- Keep the njord service project untouched — all new code lives in the AppHost.

**Non-Goals:**
- ServiceDefaults, Serilog, or OpenTelemetry integration (deferred to "observability" change).
- Production deployment orchestration (Aspire publish, Azure Container Apps, etc.).
- Changes to the njord Docker image or Dockerfile.

## Decisions

### D1: Aspire 13 SDK with direct package references

Use `Aspire.AppHost.Sdk/13.0.0` as the project SDK. Add `Aspire.Hosting.PostgreSQL` (13.4.6) for the PostgreSQL toggle. No Aspire client integrations are added to the njord service project — the AppHost only orchestrates.

**Why not Aspire client integrations in njord?** The service already has its own config binding (`NjordOptions`), MQTT client (`MqttNetPublisher`), and persistence wiring. Adding Aspire client packages would couple the service to Aspire, which conflicts with the standalone Docker deployment model. The AppHost injects config via environment variables, which the existing `IConfiguration` binding picks up transparently.

### D2: Mosquitto as `AddContainer` with bind-mounted config

Mosquitto v2 rejects anonymous connections without explicit configuration. A minimal `mosquitto.conf` (two lines: `listener 1883` + `allow_anonymous true`) is checked into the AppHost project directory and bind-mounted into the container.

**Alternative considered:** Environment variables. Mosquitto does not support config via env vars — a config file is the only option.

### D3: MQTT Explorer via `AddContainer` with env-var wiring

The `smeagolworms4/mqtt-explorer` image accepts connection config via environment variables (`CONFIG_CONNECTIONS_0_HOST`, `CONFIG_CONNECTIONS_0_PORT`, `CONFIG_CONNECTIONS_0_NAME`). The Mosquitto endpoint host and port are injected using Aspire endpoint expressions.

### D4: PostgreSQL toggle via `IConfiguration` + launch profiles

The AppHost reads `UsePostgres` from configuration. Two `launchSettings.json` profiles control this:
- **`sqlite`** (default): No PostgreSQL container, njord uses SQLite at `data/njord-journal.db`.
- **`postgres`**: Starts a PostgreSQL container, injects `Njord__Persistence__Provider=PostgreSql` and the connection string into njord.

**Why configuration, not `ExecutionContext`?** Both profiles are "run mode" — the distinction is which backing store to use, not local-vs-published.

### D5: Rename docker-compose.yml → docker-compose.example.yml

The file becomes a reference template. Every configurable option appears as a commented environment variable with its default value. Required values (`Mqtt__Host`) are uncommented. A `njord-data` named volume maps the SQLite journal path.

### D6: No ServiceDefaults project

The Aspire dashboard still shows logs and container status without ServiceDefaults. OpenTelemetry traces/metrics and structured logging (Serilog) are a larger integration effort scoped to the "observability" change. Shipping the AppHost without ServiceDefaults avoids throwaway wiring.

## Risks / Trade-offs

- **MQTT Explorer image stability** — `smeagolworms4/mqtt-explorer` is a community image, not an official product. → Mitigation: pin the image tag; the container is dev-only and easily removable.
- **Aspire 13 on .NET 10** — Aspire 13 targets net8.0+ and is confirmed compatible with .NET 10. No risk identified.
- **No health check integration yet** — Without ServiceDefaults, `WaitFor(mosquitto)` relies on container readiness, not application-level health. → Acceptable for dev; proper health checks come with the observability change.
- **PostgreSQL connection string format** — Aspire generates its own connection string format. njord's `PersistenceOptions.ConnectionString` needs to accept Npgsql-style strings. → The existing `string? ConnectionString` property already does; no code change needed.
