## Why

njord is configured entirely through environment variables and `appsettings.json`. Changing a threshold, adding a location, or enabling an enrichment feature requires editing env vars and restarting the container. There is no visibility into pipeline health, API budget consumption, or per-model fetch activity beyond what OTLP exports to an external collector (which most self-hosted HA users don't run). A lightweight admin UI inside the container would make njord self-serviceable — configure and monitor without external tooling.

## What Changes

- **Runtime config editing** — a Vue.js SPA served from the same container on `:8080` that reads and writes njord settings through a REST API. Changes take effect immediately via `IOptionsMonitor<T>` without container restart.
- **Hot model/location management** — adding or removing weather models and locations updates the live pipeline (SchedulerActor, DiscoveryActor) without tearing down the Akka.Streams graph, which is already model-agnostic.
- **In-process stats collection** — a ring-buffer service taps existing `NjordTelemetry` counters and exposes windowed time-series data for dashboard graphs (poll activity, fetch latency, MQTT throughput, API budget usage).
- **ASP.NET API surface** — new `/api/config`, `/api/stats`, `/api/health` endpoints backing the SPA.
- **Dockerfile change** — additional Node build stage for the Vue app; static output copied into `wwwroot/`.

### API budget impact

Zero additional Open-Meteo requests. The UI only reads/writes local config and in-process stats. Model/location changes affect future poll scheduling but the budget guard (80% of monthly limit) still validates before applying.

## Capabilities

### New Capabilities

- `writable-config-provider`: In-memory configuration overlay with `IOptionsMonitor` integration and optional file-backed persistence for restart survival.
- `config-change-coordinator`: Service that diffs `NjordOptions` snapshots on change, validates, and dispatches actor messages (`AddTargets`/`RemoveTargets`, `RefreshDiscovery`) for hot model/location updates.
- `admin-api`: ASP.NET minimal API endpoints for config CRUD, stats time-series, and health/status queries.
- `stats-collector`: In-process ring-buffer service that captures `NjordTelemetry` counter/histogram snapshots at regular intervals for dashboard consumption.
- `admin-spa`: Vue 3 + TypeScript SPA with config editor forms and Chart.js-based stats dashboard, built as static files and served via `UseStaticFiles`.
- `hot-model-lifecycle`: SchedulerActor and DiscoveryActor extensions for runtime add/remove of poll targets and discovery entries without pipeline restart.

### Modified Capabilities

- `service-configuration`: `NjordOptions` binding switches from `IOptions<T>` to `IOptionsMonitor<T>` in actors that need hot-reload; `WritableMemoryConfigurationProvider` added as last provider.
- `poll-scheduler`: SchedulerActor gains `AddTargets`/`RemoveTargets` persistent commands and weight recomputation.
- `enrichment-actor`: Captured `locations` list replaced with live reference from `IOptionsMonitor`.
- `mqtt-actor-topology`: DiscoveryActor gains `RefreshDiscovery` with diff-based publish and tombstone logic for removed entities.

## Non-goals

- **Authentication/authorization** — the UI runs on the same port as health endpoints, private-network assumption (same as HA). Auth can be added later if needed.
- **WebSocket/SSE push** — v1 dashboard uses polling (`setInterval` + fetch). Real-time push is a future enhancement.
- **Forecast data preview** — showing actual forecast values in the UI would require a new query path from actor state. Out of scope.
- **Metrics persistence** — stats are ephemeral (container restart = reset). Durable metrics belong in an external OTLP stack.
- **Hot-reload of MQTT connection or persistence provider** — these require container restart and are labeled as such in the UI.

## Impact

- **New projects/files**: Vue app under `src/Njord/ui/` (or top-level `ui/`), API controllers/endpoints in `src/Njord/Api/`, `WritableConfigProvider` + `ConfigChangeCoordinator` + `StatsCollector` services.
- **Modified actors**: `SchedulerActor`, `DiscoveryActor`, `EnrichmentActor` — new message types and `IOptionsMonitor` adoption.
- **Modified config setup**: `NjordServiceSetup` adds the writable provider; actors switch from `IOptions` to `IOptionsMonitor` where needed.
- **Dockerfile**: New Node build stage, `COPY` of SPA dist to `wwwroot/`.
- **Dependencies**: Vue 3, Chart.js, TypeScript (build-time only; no Node runtime in container). Possible new NuGet package: none expected (ASP.NET minimal APIs are in-box).
- **Test impact**: New unit tests for `WritableConfigProvider`, `ConfigChangeCoordinator`, actor command handlers. Integration tests for API endpoints. Vue component tests (vitest).
