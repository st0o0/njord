## Why

njord's configuration is currently static — defined in `appsettings.json` and immutable at runtime. Any change (adding a location, toggling an enrichment, adjusting thresholds) requires editing the config file and restarting the container. For the ha-njord companion and for a future njord Web UI, runtime configuration is essential: users should be able to add locations, enable enrichments, and tune alert thresholds without touching files or restarting services.

The gRPC `ConfigService` already exposes `GetConfig` and `StreamConfig` (read-only). This change adds mutation RPCs for all user-facing configuration — locations, models, forecast settings, enrichment parameters, and budget overrides. Infrastructure settings (MQTT, persistence, ports) stay in `appsettings.json` / environment variables.

## What Changes

- **6 mutation RPCs** on `ConfigService`:
  - `AddLocation`, `RemoveLocation`, `UpdateLocation` — location lifecycle management with budget validation.
  - `UpdateForecastSettings` — poll interval, horizons, forecast days, parameter groups, default models.
  - `UpdateEnrichmentConfig` — per-feature enable/disable plus all configurable thresholds and parameters (alert thresholds, energy temps, index base temps, consensus method, history retention).
  - `UpdateBudget` — override the free-tier request budget for self-throttling.
- **`GetStatus` RPC** — server version, uptime, budget usage (monthly/daily), per-model fetch status.
- **`ConfigResponse`** — every mutation returns the new `NjordConfig` plus a `BudgetProjection` (projected monthly calls, % of limit, within_budget flag) and optional warnings/rejection.
- **Enrichment detail messages** in proto — `ConsensusConfig`, `AlertConfig` (with all thresholds), `EnergyConfig`, `IndexConfig`, `HistoryConfig`, `DerivedConfig`, `TrendConfig`. `NjordConfig` expands to include the full enrichment details, not just enabled flags.
- **Config persistence** — mutations persist to `njord-config.json` (separate from `appsettings.json`). On startup, njord merges: `appsettings.json` (defaults) ← `njord-config.json` (user overrides).
- **Config propagation** — after persisting, mutations trigger `IOptionsMonitor` change notifications so actors react without restart.

## Non-goals

- MQTT settings via gRPC — MQTT host, port, credentials, topics, discovery prefix stay in `appsettings.json` / env vars.
- Persistence/gRPC port settings via gRPC — infrastructure stays infrastructure.
- Hot-reload of parameter groups changing the active parameter set mid-pipeline (partial restart may be needed — documented but not fully automated in this change).
- Web UI — this provides the API; consumers come separately.

## Capabilities

### New Capabilities

- `config-mutation-api`: gRPC mutation RPCs for runtime configuration changes with budget validation, persistence, and propagation.
- `server-status-api`: `GetStatus` RPC exposing server version, uptime, budget usage, and per-model health.
- `config-persistence`: File-based persistence of user config overrides in `njord-config.json` separate from deployment config.

### Modified Capabilities

- `grpc-config-service`: Extends the existing read-only `ConfigService` with mutation RPCs and `GetStatus`. `NjordConfig` message expands to include full enrichment details.

## Impact

- **Proto**: Major expansion of `config_service.proto` — ~20 new messages, 7 new RPCs.
- **ConfigGrpcService**: Grows significantly with mutation handlers, validation, persistence, and propagation logic.
- **New file**: `njord-config.json` persistence layer.
- **NjordOptions**: Needs to support merging from two sources (appsettings + config file).
- **Actors**: React to `IOptionsMonitor` changes — `SchedulerActor` (poll interval, locations), `DiscoveryActor` (horizons, models), enrichment features (enabled flags, thresholds).
- **API budget**: Zero impact on Open-Meteo calls — mutations only change what njord does on the next poll cycle.
