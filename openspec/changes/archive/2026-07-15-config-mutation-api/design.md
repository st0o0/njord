## Context

njord's config is currently bound once at startup via `IOptions<NjordOptions>`. The gRPC `ConfigService` has read-only `GetConfig`/`StreamConfig`. Actors use `IOptions<NjordOptions>` (snapshot at startup) or `IOptionsMonitor<NjordOptions>` (reactive). The infrastructure for change propagation exists (`IOptionsMonitor.OnChange`) but no mutation path feeds it.

The `ConfigGrpcService` already uses `IOptionsMonitor<NjordOptions>` and `StreamConfig` listens for `OnChange` events. The plumbing for reactive config is in place — we need to add the write side.

## Goals / Non-Goals

**Goals:**

- Expose all user-facing configuration via gRPC mutation RPCs.
- Persist user config overrides in `njord-config.json` (merged with `appsettings.json` at startup).
- Trigger `IOptionsMonitor` change notifications after mutations so actors react without restart.
- Budget-validate every mutation that affects API call volume.
- Expose server health and budget usage via `GetStatus`.

**Non-Goals:**

- MQTT or infrastructure settings via gRPC.
- Automated pipeline restart for parameter group changes (document as requiring restart).
- Web UI (API only).

## Decisions

### D1: Config persistence in njord-config.json

**Decision:** User-initiated config changes persist to `data/njord-config.json`. On startup, the standard .NET configuration chain loads `appsettings.json` then overlays `njord-config.json`. Both bind to `NjordOptions`.

**Rationale:** Separating user overrides from deployment defaults means `appsettings.json` can be read-only (baked into the Docker image or mounted read-only), while `njord-config.json` lives in the writable `/app/data` volume. The standard `ConfigurationBuilder.AddJsonFile` with `reloadOnChange: true` feeds `IOptionsMonitor` automatically.

**Alternatives considered:**
- SQLite for config → overkill for a small JSON blob.
- Write back to `appsettings.json` → dangerous (deployment config, might be read-only mount).
- Actor persistence (Akka journal) → config is not actor state; it's application-level.

### D2: Mutation → persist → reload → propagate

**Decision:** Each mutation follows this pipeline:
1. **Validate** — field validation + budget projection.
2. **Persist** — write `njord-config.json` atomically (write temp + rename).
3. **Reload** — the `reloadOnChange` file watcher triggers `IOptionsMonitor.OnChange`.
4. **Propagate** — actors subscribed to `IOptionsMonitor` react.
5. **Respond** — return new `NjordConfig` + `BudgetProjection`.

**Rationale:** This leverages .NET's built-in config reload. No custom event bus needed. The file write triggers the exact same `OnChange` that `StreamConfig` already listens on — all consumers get the update automatically.

### D3: Budget validation rejects at 100%, warns at 80%

**Decision:** Every mutation that changes API call volume computes the projected monthly calls: `models_per_cycle × api_call_weight × cycles_per_month`. If projected > monthly limit: reject with `applied = false`. If > 80%: accept with a warning in `ConfigResponse.warnings`.

**Rationale:** The Open-Meteo free tier is a soft limit (300k/month). njord should be conservative — rejecting budget-busting config changes prevents accidental API abuse. The 80% threshold gives early warning.

### D4: NjordConfig message expands to include enrichment details

**Decision:** The `NjordConfig` proto message gains full enrichment sub-messages (`ConsensusConfig`, `AlertConfig`, etc.) replacing the `EnrichmentConfig` with just enabled flags. `GetConfig` returns the complete picture.

**Rationale:** A config API that can set thresholds but can't read them back is broken. The consumer needs to know the current alert thresholds before changing them. Full round-trip fidelity.

### D5: Parameter group changes documented as requiring restart

**Decision:** `UpdateForecastSettings` can change `parameters.groups/extra/exclude`. This changes which variables njord requests from the API. The `ResolvedParameterSet` is a DI singleton resolved once. Changing it at runtime would require re-resolving and propagating to all consumers (ModelStateActor, MqttEgressActor, HorizonProjection, enrichments). This is complex. For this change: the mutation persists and warns `"Parameter changes take effect after restart"`. Full hot-reload is a follow-up.

**Rationale:** All other mutations propagate via `IOptionsMonitor` naturally (actors read values on each cycle). Parameter groups are the exception because `ResolvedParameterSet` is pre-computed. Supporting hot-reload for this one field would require significant refactoring of how parameters flow through the system.

### D6: GetStatus aggregates from existing health infrastructure

**Decision:** `GetStatus` returns version (from `AssemblyInformationalVersionAttribute`), uptime (from `NjordHealthState.ServiceStartedUtc`), budget usage (from a new `BudgetTracker` singleton), and per-model status (from `SchedulerActor` via Ask).

**Rationale:** Most of this data already exists scattered across the system. `GetStatus` aggregates it into one response. The `BudgetTracker` is new but simple — it counts API calls and resets monthly/daily.

## Risks / Trade-offs

**[Risk] Config file corruption** → Atomic write (temp + rename) prevents partial writes. On read failure, fall back to `appsettings.json` defaults.

**[Risk] Race condition: two concurrent mutations** → The config file write is single-writer (ConfigGrpcService is a singleton, mutations are serialized via `SemaphoreSlim(1)`). No concurrent write risk.

**[Risk] Parameter group hot-reload not supported** → Documented as restart-required. Users see a warning in ConfigResponse. All other mutations propagate without restart.

**[Trade-off] Proto size** → ~20 new messages. The proto file grows significantly. Mitigated by clear section organization and the fact that config messages are used infrequently (not in hot paths).

## Open Questions

None.
