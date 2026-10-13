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

#### Inventory (task 1.1)

`NjordOptions` (section `Njord`), production readers per property:

| Property | Key | Readers |
|---|---|---|
| `Locations` | `Njord:Locations` | `PollScheduler` (init, trigger, cycle totals), `OpenMeteoGateway` (`FindLocation`), `IndexAnalyzer` (location names for the preference map), `WeatherGrpcService`, `SensorGrpcService`, `AdminGrpcService`, `BudgetCalculator.Validate`, `NjordOptionsValidator` |
| `Models` | `Njord:Models` | `PollScheduler`, `WeatherGrpcService`, `AdminGrpcService`, `BudgetCalculator.Validate`, `NjordOptionsValidator` |
| `Horizons` | `Njord:Horizons` | `DerivedAnalyzer`, `MqttStatePublisher`, `AdminGrpcService`, `NjordOptionsValidator` |
| `ForecastDays` | `Njord:ForecastDays` | `OpenMeteoClient` (request), `OpenMeteoGateway` (call weight), `MqttStatePublisher`, `AdminGrpcService` |
| `Parameters` | `Njord:Parameters` | `CoreSetupContainer` (`ResolvedParameterSet` singleton), `BudgetCalculator.Validate`, `AdminGrpcService`, `NjordOptionsValidator` |
| `PollInterval` | `Njord:PollInterval` | `BudgetCalculator.Validate` (projection only), `PipelineHealthCheck` (staleness), `AdminGrpcService`, `NjordOptionsValidator`. **Not** read by `PollScheduler` |
| `BudgetOverride` | `Njord:BudgetOverride` | `BudgetCalculator.GetEffectiveBudget` → `OptionsBudgetProvider` (gateway budget gate, live), `OpsGrpcService.GetStatus` (live), `CoreSetupContainer` (health monitor seed), `AdminGrpcService` |
| `DiscoveryInterval` | `Njord:DiscoveryInterval` | `PollScheduler` (discovery-phase poll delay, miss/transient fallback delay), `NjordOptionsValidator` |
| `OpenMeteoBaseUrl` | `Njord:OpenMeteoBaseUrl` | `OpenMeteoSetupContainer` (HttpClient base address) |
| `Persistence` (`PersistenceOptions`) | `Njord:Persistence` | `AkkaSetupContainer` (SQL persistence), `NjordOptionsValidator` (PostgreSQL connection string, writable dir) |
| `PersistencePath` | `Njord:PersistencePath` | `AkkaSetupContainer`, `NjordOptionsValidator` (writable probe) |
| `Mqtt` (`MqttOptions`) | `Njord:Mqtt` | `NjordOptionsValidator` (host check) only; every MQTT consumer already injects the standalone `IOptions<MqttOptions>` |
| `Enrichment` (`EnrichmentOptions`) | `Njord:Enrichment` | `OpsGrpcService` (active enrichments), `AdminGrpcService` (map/write); analyzers use the dual-bound sub-types |

`EnrichmentOptions` (`Njord:Enrichment`, also bound standalone in `AnalysisSetupContainer`
with `ConsensusOptionsValidator` and `IndexOptionsValidator` registered against it):

| Property | Readers |
|---|---|
| `Consensus` (`Enabled`, `Method`, `TrimPercent`) | `OpsGrpcService`, `AdminGrpcService` only. `ConsensusSnapshotFactory` uses its own default trim percent (0.1); no compute code reads consensus options |
| `Alerts` (`AlertOptions`) | `AlertAnalyzer` → `AlertEvaluator.EvaluateAll` (via standalone binding), Ops/Admin |
| `Derived` (`Enabled`) | `DerivedAnalyzer` (standalone binding), Ops/Admin |
| `Trends` (`Enabled`) | `TrendAnalyzer` (standalone binding), Ops/Admin |
| `Indices` (`IndexOptions`) | `IndexAnalyzer` → `PreferenceResolver.Resolve` (in Compute) at construction, Ops/Admin |

`WritableNjordOptions` (`IWritableOptions<NjordOptions>`: `Value`, `Update(Action<T>)`),
registered in `CoreSetupContainer` for `data/appsettings.Override.json`. `Update` takes a
synchronous `SemaphoreSlim`, deep-clones `CurrentValue` twice via `System.Text.Json`,
applies the mutation to one clone, computes a JSON delta (changed leaves; arrays replaced
whole; removed keys written as `null`), merges the delta into the existing file's `Njord`
object, writes `{"Njord": …}` to `<path>.tmp` and moves it over the file (atomic), calls
`IConfigurationRoot.Reload()` and returns the new `CurrentValue`. No validation happens
before the write. Its only consumer is `AdminGrpcService`.

Other findings: there is no `GrpcOptions` type and no gRPC configuration key; there is no
`NjordActorSystemSetup` (actors register through `IActorRegistration` implementations that
`AkkaSetupContainer` applies); `AlertEvaluator` lives in `Njord.Alerts`, not Compute.

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

## Deviations

Recorded during implementation; each is the smallest change that kept the plan workable.

1. **Stale test folders not deleted (task 1.2).** Deleting `src/Njord.Tests/bin|obj`,
   `src/Njord.Domain.Tests` and `src/Njord.Egress.Tests` was refused by the tool permission
   layer. The new `Njord.Tests.csproj` builds over the old `bin/obj`; the two leftover folders
   have no project file, so the test loop and the solution ignore them. They remain for a manual
   `rm -rf`.
2. **`NjordOptions` keeps only the forecast scope, so three more split types exist.**
   `DiscoveryInterval` moved to `PollingOptions` (it is the scheduler's discovery-phase poll
   delay, the only interval `PollScheduler` actually reads), `OpenMeteoBaseUrl` to
   `OpenMeteoOptions` (section `Njord`, registered by `OpenMeteoSetupContainer`),
   `PersistencePath` to `StorageOptions` (section `Njord`). `Persistence` became the standalone
   `PersistenceOptions` (`Njord:Persistence`). All keys are unchanged (D1). There is no
   `GrpcOptions`: no gRPC configuration key exists.
3. **`BudgetCalculator`/`RequestBudget` moved to `Configuration/Polling/`.** The 80 % guard moved
   from `NjordOptionsValidator` to `PollingOptionsValidator`, which reads the forecast scope from
   `IConfiguration` (not `IOptions<NjordOptions>`, which would throw inside a validator when the
   scope itself is invalid). The admin API applies the same guard as a pre-check for location,
   model and parameter changes; it used to reject only above 100 %.
4. **Configured collections now replace their defaults.** The configuration binder appends
   configured items to collections that already hold defaults (`Horizons`, `Parameters.Groups`,
   every alert threshold array). Before this change configured thresholds were silently ignored
   (the first three, i.e. the defaults, were used) and configured horizons were merged with the
   default ladder. The new `AlertOptionsValidator` (three monotonic values) would have failed on
   that, so `ReplaceCollectionDefaults()` (Core) rebinds configured collections fresh after
   binding. Behavior change for users who configured these keys: their values now apply.
   Not fixed: index-wise merging of arrays between configuration *sources* (an override file with
   fewer `Locations` than `appsettings.json` keeps the trailing base entries). It predates this
   change and affects the old writer equally.
5. **Task order: 5.1 and most of 6.3 were done in group 2.** Removing `NjordOptions.Enrichment`,
   `PollInterval` and `BudgetOverride` (2.4) breaks the NjordOptions-wide writer, so the
   section writer (`IConfigWriter`/`ConfigWriter`) and the admin service rewrite landed with
   group 2. `IConfigWriter` also has `Check<T>` (validate without writing) so a multi-section
   admin request is validated as a whole before anything is persisted.
6. **Non-actor readers of current values use `ConfigView`.** Admin `GetConfig`, Ops `GetStatus`
   and the pipeline health check need the persisted, current values (the admin spec expects a
   write to be visible in the next `GetConfig`). They read them per call through `ConfigView`
   (Core), which wraps the `IOptionsMonitor<T>` instances and never subscribes.
   `StreamConfig` listens to `ConfigChangeFeed` instead of `IOptionsMonitor.OnChange`.
7. **`ConsensusOptions.MinQuorum` added (default 2).** The spec scenarios use it, but it did not
   exist; `ConsensusSnapshotFactory` hard-coded a quorum of 2. The validator rejects values
   below 1. Default output is unchanged.
8. **Nested value types are not section types.** `ParameterOptions` (in `NjordOptions`) and
   `LocationOptions` (in Njord.Weather) stay nested; they carry no `SectionName` and are
   validated by `NjordOptionsValidator`. The "no options type nested in another" rule is applied
   to section-bound types.
9. **Validators for toggle-only types.** `TrendOptions` and `DerivedOptions` only hold `Enabled`;
   their validators always succeed, to keep "exactly one validator per options type".
10. **Settings details (group 3).** `ConsensusMethod` moved to `Njord.Compute/Settings/` (it is
    part of `ConsensusSettings`), so Compute has no `Configuration/` folder. `PreferenceResolver`
    moved to `Njord.Core/Configuration/Indices/` in group 2 because it reads `IndexOptions`; its
    spec moved to `Njord.Core.Tests`. `TrendSettings` has no members (trend computation has no
    parameters); `TrendComputer` does not take it, only `TrendAnalyzer` does.
    `ConsensusSnapshotFactory.Create` now takes `ConsensusSettings` and uses its `TrimPercent`
    and `MinQuorum` (before: hard-coded 0.1 and 2, i.e. the configured `TrimPercent` was ignored).
    Defaults are identical, so the golden masters are unchanged. `IndexComputer.Compute` takes
    `IndexSettings` and `DerivedResultComputer.Compute` takes `DerivedSettings`.
11. **Pipeline seed and messages (group 4).** The startup `AnalysisSettings` is a DI singleton
    built by `PipelineSetupContainer` from the validated options (`AnalysisSettingsFactory`);
    `ForecastPipeline` takes it as a constructor argument. `AnalysisSettingsChanged` and
    `PollingChanged` live in `Njord.Core/Configuration/ConfigChangeMessages.cs`, because
    `Njord.Messages` cannot reference Compute's settings records. A "cycle" in the pipeline is one
    forecast element: the settings are zipped onto each element, so one computation never mixes
    two settings versions; the spec shows the switch on the next element.
