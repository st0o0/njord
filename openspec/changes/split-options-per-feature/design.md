## Context

See proposal.md for motivation. Current state, from the options-usage inventory:

| Consumer | Injects | Effect |
|---|---|---|
| `OpenMeteoGateway` | `IOptions<NjordOptions>` and `IOptionsMonitor<NjordOptions>` | mixed |
| `GatewayBudgetGate` | `IOptionsMonitor<NjordOptions>` | live |
| `PollScheduler`, `OpenMeteoClient`, `MqttStatePublisher`, `PipelineHealthCheck` | `IOptions<NjordOptions>` | frozen at start |
| `AlertAnalyzer`, `IndexAnalyzer`, `TrendAnalyzer`, `DerivedAnalyzer` | `IOptions<T>` (+ `NjordOptions`) | frozen at start |
| `AdminGrpcService` | `IOptionsMonitor` + `IWritableOptions<NjordOptions>` + `OnChange` | writes everything |
| `OpsGrpcService`, `WeatherGrpcService`, `SensorGrpcService` | `NjordOptions` | read |
| `AkkaSetupContainer`, `OpenMeteoSetupContainer`, `CoreSetupContainer` | `GetRequiredService<IOptions<…>>().Value` at registration | frozen at start |

Enrichment sections are bound twice (`EnrichmentOptions` + each sub-type). The consensus
and index validators are registered as `IValidateOptions<EnrichmentOptions>`.

Note for the implementer: the full property list of `NjordOptions` and the internals of
`WritableNjordOptions` were not read while planning (a tooling restriction blocked the
read). Task 1.1 inventories them before anything moves.

## Goals / Non-Goals

**Goals:** one binding per section, narrow injection, one well-defined path for runtime
changes, and Compute free of options.

**Non-Goals:** changing configuration keys, changing the admin proto, hot-applying
restart-required settings.

## Decisions

### D1: Narrow options types may share a section
`PollingOptions` (PollInterval, BudgetOverride) and `NjordOptions` (forecast scope) both
bind `Njord`. The binder ignores keys a type does not declare, so the JSON and env vars
stay unchanged. *Alternative:* move keys under new sub-sections (`Njord:Polling:…`).
Rejected because it breaks existing configs, docker-compose files and the ha-njord docs.

### D2: Options types stay in `Njord.Core/Configuration/<Area>/`
Feature libraries, the host's `ConfigManager` and the admin API all need to read the
options types, and feature libraries cannot reference each other. So the types live in
Core, grouped by area folder with their validator. The owning feature's setup extension
does the `AddOptions<T>().BindConfiguration().ValidateOnStart()` registration.
*Alternative:* options inside each feature library. Rejected because the admin API (which
references Core only) would need to see every feature's options.

### D3: `ConfigManager` actor in the host
`src/Njord/Configuration/ConfigManager.cs`, resolved by the marker `IConfigManager` in
`Njord.Core/Actors/ActorKeys.cs`.
- It subscribes to `IOptionsMonitor<T>.OnChange` for the hot types only. Callbacks run on
  a foreign thread and only do `Self.Tell(new XxxOptionsReloaded(value))`.
- On reload it converts options to settings records and `Tell`s typed messages to its
  consumers: `AnalysisSettingsChanged` → `IForecastPipeline`, `PollingChanged` →
  `IPollScheduler`.
- It owns the section writer for runtime updates. For a restart-required section it adds
  the restart warning to its reply.

*Alternative:* each actor subscribes to `IOptionsMonitor` itself. Rejected because
`OnChange` callbacks run outside the actor, and actors would race against each other's
view of the config.

### D4: Pipeline gets settings through a side channel
`ForecastPipeline` already merges sensor data through `Source.ActorRef` + `ReuseLatest`.
Analysis settings use the same pattern. The pipeline is seeded with the startup settings,
and `AnalysisSettingsChanged` is pushed into the settings channel. The per-cycle
computation reads the latest settings, so a change applies on the next cycle and never
in the middle of one.

### D5: Non-actor hot readers use `CurrentValue` per call
`GatewayBudgetGate` runs inside a stream stage, not an actor. It reads
`IOptionsMonitor<PollingOptions>.CurrentValue` per call and never subscribes. This is the
only allowed `IOptionsMonitor` use outside `ConfigManager`, and the architecture spec
lists it explicitly.

### D6: Section-scoped writer replaces `IWritableOptions<NjordOptions>`
`IConfigWriter.UpdateAsync<T>(Action<T>, CancellationToken)` keeps the existing override
file mechanics (section-wrapped JSON, arrays replaced, atomic write, `SemaphoreSlim`,
created on first write). It resolves the section path from `T.SectionName`, validates with
the `IValidateOptions<T>` instances from DI, and returns either success or the failures.

### D7: Classification lives in one place
A static `ConfigReloadPolicy` in Core maps every options type (and, for `NjordOptions`,
every property) to Hot or RestartRequired. `ConfigManager` and the admin reply use it, and
a spec asserts that every bound options type is classified.

## Risks / Trade-offs

- [The admin API now reports "restart required" for locations, models and horizons] → This
  makes today's silent half-apply explicit. The ha-njord integration should show the
  warning, so check its handling of `warnings` before release.
- [Feature `Enabled` toggles move to restart-required] → Toggling a feature changes the
  entity set the HA integration sees. A restart is the safe boundary. Revisit if users ask.
- [A budget change via admin and a concurrent poll] → The budget gate reads `CurrentValue`
  per call, so the worst case is one request evaluated against the old budget.
- [Bigger DI surface: many small options types] → Each feature registers its own, so the
  host shrinks rather than grows.

## Migration Plan

No config migration: keys are unchanged. An existing `data/appsettings.Override.json` keeps
working because its paths are the section paths. Rollback is reverting the commit; the
override file format is unchanged.
