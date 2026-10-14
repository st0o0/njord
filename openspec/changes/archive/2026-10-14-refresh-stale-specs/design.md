## Context

See `proposal.md` (Why). The audit compared every main spec with `src/` (types, files,
registrations, behavior), not with the archived changes. The most recent intended state
came from the archived changes `2026-10-12-*` to `2026-10-14-*`; several of them were
archived without syncing their deltas (for example `2026-10-12-gateway-pipeline-redesign`
targeted capabilities that never reached `openspec/specs/`, and the `2026-10-14` pipeline
changes still describe `AnalysisResult`/`PollContext`, which were later removed). Where an
archived delta and the code disagree, the code wins.

Constraints:

- A MODIFIED block must carry the full requirement and every scenario name of the main
  requirement; the archive rejects a MODIFIED block that drops one.
- Delta specs cannot change a capability's `## Purpose`.
- `openspec` 1.7.0 refuses to write a rebuilt spec with zero requirements and has no
  "remove capability" operation.

## Goals / Non-Goals

**Goals:**

- Every requirement in `openspec/specs/` names only types, files and projects that exist,
  and describes behavior the code has (or is listed below as a known deviation).
- Capabilities whose subject is gone disappear, with a recorded reason and migration.
- The archive merge succeeds and `openspec validate --all` passes afterwards.

**Non-Goals:**

- Fixing the code deviations listed below (each needs its own change).
- Restructuring the capability list (merging overlapping specs, renaming capabilities).
- Rewording correct requirements.

## Decisions

### D1 — Code is the source of truth, with listed exceptions

Specs are restated to match `src/`. The exception is a requirement that still expresses a
product decision recorded in `AGENTS.md` or the API contract, where the code has an
evident defect; those requirements keep the intended behavior and are listed in "Known code
deviations" so they are not mistaken for drift. Alternative: restate them to match the
code — rejected, it would turn defects into requirements.

### D2 — MODIFIED only when nothing obsolete remains

A requirement is MODIFIED when its name still fits and every existing scenario stays true
after rewording. When the name names a removed type (`SchedulerActor …`,
`BudgetTrackerActor …`) or a scenario is obsolete, the requirement is REMOVED (reason:
restated against the current code; migration: the new requirement) and ADDED under a new
name. Pure renames were not used: in every rename case at least one scenario also changed.

### D3 — Whole capabilities are removed through REMOVED deltas plus a manual step

The 12 capabilities without a subject get a delta that removes every requirement, so the
reason and migration are archived with the change. Because `openspec archive` validates
each rebuilt spec and an empty spec fails ("Spec must have at least one requirement"), the
archive runs with `--no-validate` after `openspec validate refresh-stale-specs` has passed,
and the 12 emptied spec folders are deleted right after. A dry run in a scratch copy
confirmed: the archive merges all 107 deltas (+109 / ~158 / −206 requirements), exactly
these 12 specs end up empty, and after deleting them `openspec validate --all` passes
(138 specs). Alternatives: delete the main spec folders first (the archive then "creates"
them from REMOVED-only deltas and fails the same way); drop the delta files (loses the
recorded reasons).

### D4 — Gateway and budget content stays in existing capabilities

The unsynced `forecast-gateway`, `budget-domain` and `actor-domain-naming` capabilities of
`2026-10-12-gateway-pipeline-redesign` are not created. Their substance is restated in
`poll-pipeline` (gateway fetch stream), `poll-scheduler`, `dynamic-budget-throttle` and
`budget-tracker-persistence`, whose purposes already cover it.

### D5 — Purpose sections are fixed after the archive

Deltas cannot touch `## Purpose`, so the stale or `TBD` purposes are rewritten as a task
after the archive (listed in `tasks.md`).

### D6 — E2E: only njord-side facts change

`ha-njord` is not available in this workspace, so entity counts and entity names in the
E2E specs are not re-derived. Only the njord facts (no broker, five analysis features, no
history payload, repo-relative paths, three subagents) are corrected.

## Audit

Evidence paths are under `src/` unless stated otherwise.

### Rewritten capabilities

| Capability | What was wrong | Evidence |
|---|---|---|
| pipeline-composition | `PipelineActor` graph with KeepAlive/IdleTimeout, `PollContext`, inline history, central registration, `ModelCapability` | `Njord.Pipeline/ForecastPipeline.cs:186-270` (ZipWith + `StatefulSelectMany` + `BroadcastHub(2)`), no KeepAlive; registrations in `*/Configuration/*SetupContainer.cs` |
| consumer-model | `AnalysisResult`, `GrpcConsumerActor`, IdleTimeout cleanup, `AddMqttConsumer()` | `Njord.Messages/Pipeline/PipelineEvent.cs`, `Njord.Pipeline/ForecastReadModel.cs:97-160`, `Njord.Mqtt/Configuration/MqttSetupContainer.cs:28-52` |
| poll-pipeline | MergeHub/SinkRef/`WeightedTarget`, `Buffer(32)`, `BudgetThrottleStage`, EgressActor consumer | `Njord.OpenMeteo/OpenMeteoGateway.cs:62-148` (queue 16, `SelectAsyncUnordered(2)`, `Sink.ActorRef`, forecast queue → `BroadcastHub(2)`) |
| poll-scheduler | `PollSchedulerActor`→`PipelineActor`, local queue, `HashResult`, `SchedulerSnapshotDto`, `QueryPollStatesResult`, `TriggerPollResult` | `Njord.Pipeline/PollScheduler.cs:55-160,186-290` (`FetchForecast`, `FetchForecastCompleted` + `Ack`, `WaitingForGateway`) |
| pipeline-commands | `WeightedTarget`, `RefreshLocation`, shared CycleId per refresh | `Njord.OpenMeteo/OpenMeteoGateway.cs:45,82-84,151` |
| poll-status-query | `SchedulerMessages.cs`, `Njord.Pipeline` namespace, string location | `Njord.Messages/Pipeline/QueryPollStates.cs`, `PollStateEntry.cs` |
| failure-routing | `FetchOutcome.Failure` without identity; transport increments `missCount`; no retry for ModelUnavailable/Malformed | `Njord.Weather/PollOutcome.cs`; `Njord.Pipeline/PollScheduler.cs:268-293` (all reasons → `ApplyTransientFailure`) |
| dynamic-budget-throttle | `IBudgetGate<T>`, `WeightedBudgetGate`, `BudgetThrottleStage`, burst 4 | `Njord.OpenMeteo/GatewayBudgetGate.cs:12-200` (burst `max(cost/30,16)`) |
| location-model-resolution | `LocationOptions` resolve method, `Europe` tier, coverage warnings | `Njord.Weather/LocationOptions.cs`, `ModelCoverageRegistry.cs:3`, `Njord.Core/Configuration/NjordOptionsValidator.cs:92-112` (failure, unknown models accepted) |
| stream-composition | single `PipelineActor` graph, MergeHub, feedback consumer, `StopStreams` shutdown, log per outcome, `EgressSourceFailed` | gateway/pipeline above; `Njord.OpenMeteo/OpenMeteoClient.cs:89-104` (metrics, no log) |
| stream-consumer-actor | "no WaitingForRefs on startup"; `RequestPipelineSource` pairs | `Njord.Core/Actors/StreamConsumerActor.cs:33-39,135-151`; `StreamWiringMessages.cs` |
| producer-broadcast-pattern | PipelineActor/ModelStateActor/EnrichmentActor producers | gateway, `ForecastPipeline`, `ForecastReadModel` hubs |
| egress-stream-graph | ModelStateActor hub, publish-sink buffer, Source.Queue availability, GrpcSnapshotConsumerActor | `Njord.Mqtt/MqttConnection.cs:85-131`, `Transport/MqttNetPublisher.cs` (no buffer) |
| mqtt-actor-topology | `MqttConnectionActor` MergeHub, `MqttStateActor`, `MqttDiscoveryActor` | `Njord.Mqtt/MqttConnection.cs`, `MqttStatePublisher.cs` |
| delta-publishing | `MqttEgressActor`, `PerModelUpdate` | `Njord.Mqtt/MqttStatePublisher.cs:139-176` |
| optional-mqtt-egress | `MqttEgressActor`, `DiscoveryActor`, `EgressActor` | `Njord.Mqtt/Configuration/MqttSetupContainer.cs`, `Njord/Configuration/CoreSetupContainer.cs:54-62` |
| mqtt-egress | publishes `unavailable`, `expire_after` | no such code; `StatePayloadBuilder` omits missing values |
| model-capability-tracking | capability learning, `CapabilityLearned` | only `HorizonProjection.BuildPerHorizon` capping remains (`Njord.Weather/HorizonProjection.cs:7-65`) |
| consensus-snapshot / hourly-consensus / consensus-computation | `ConsensusSnapshot.Compute`, `ConsensusComputer`, per-location Select stage, `EgressEvent.ConsensusUpdate`, proto fields 8/16, discovery | `Njord.Compute/Consensus/ConsensusBuilder.cs`, `ConsensusStatistics.cs`; `protos/njord/v2/weather.proto` (fields 7/15) |
| model-snapshot / weather-domain / utc-timestamps | `ModelForecast`, value-based change detection, persisted forecast recovery | `Njord.Weather/Forecast.cs`, `ModelSnapshot.cs:23-35` (CycleId comparison) |
| openmeteo-client | `timezone=auto`, `TimeZoneInfo`, no `CycleId` parameter, failures without identity | `Njord.OpenMeteo/OpenMeteoClient.cs:22-125` |
| activity-indices / daily-slice-scoring / night-ventilation | `IndexScorer`, `DaySliceIndexResult`, `TimeSliceAggregator`, discovery, `hours_included` = daylight | `Njord.Indices/Scores/*Score.cs`, `DaySlicer.cs`, `DaySlice.cs` (`HoursIncluded = max`) |
| threshold-alerts / new-alert-types / severity-extensions | `AlertEvaluator.Evaluate*` per model, `double[]` parameters, discovery | `Njord.Alerts/Rules/*Rule.cs` (consensus medians), `AlertAnalyzer.cs` |
| derived-values / trend-analysis | `DerivedComputer`, `DerivedResult.Compute`, `TrendAnalyzer.PrecipitationTiming` | `Njord.Derived/DerivedFormulas.cs`, `DerivedAnalyzer.cs`, `Njord.Trends/TrendFormulas.cs` |
| daily-consensus / daily-forecast / daily-forecast-typing / enrichment-model-envelope | discovery components, history messages, per-model envelopes | `Njord.Mqtt/TopicScheme.cs`, `Njord.Indices/DaySlice.cs` |
| parameter-registry / parameter-typed-access / typed-constants / naming-collisions / structural-refactoring | `AlertEvaluator`, `HistoryAnalyzer`, `TrendResult` params, `EnrichmentKind` in Core, `TrendComputer`, `IndexComputer` | `Njord.Messages/Pipeline/EnrichmentKind.cs`, `Njord.Trends/TrendAnalyzer.cs:17-31` |
| grpc-enrichment-api / grpc-v2-weather-service / grpc-v2-admin-service / grpc-v2-ops-service / grpc-v2-common / grpc-sensor-service | `EnrichmentSnapshotActor`, `EnrichmentProtoMapper`, history payloads, "no reserved", `IWritableOptions`, `SchedulerActor`, 7 sensor kinds | `Njord.Grpc/WeatherGrpcService.cs`, `AdminGrpcService.cs`, `OpsGrpcService.cs`, `Mapping/AnalysisProtoMappings.cs`; `protos/njord/v2/*.proto` (`reserved 6`, `budget_partial`, two sensor kinds) |
| service-configuration / servus-bootstrap / domain-setup-containers | `WithResolvableActors`, Ingest/Egress/Enrichment containers, persistence inside `NjordOptions`, child actors, nested validators | `Njord/Program.cs:34-46`, `*/Configuration/*SetupContainer.cs`, `Njord.Core/Configuration/**` |
| standalone-sub-options / writable-options / config-persistence / enrichment-config-validation | `GrpcOptions`, `SensorHubActor`, `WritableNjordOptions`, `NjordOptions.Enrichment`, `HistoryOptions` | `Njord.Core/Configuration/IConfigWriter.cs`, `ConfigWriter.cs`, `Njord/Configuration/ConfigManager.cs` |
| health-checks / health-endpoint | `NjordHealthState`, `ASPNETCORE_URLS` port, "only /healthz" | `Njord.Core/Health/NjordHealthMonitor.cs`, `Njord/appsettings.json:15-26`, `NjordApplicationSetup.cs` |
| prometheus-metrics / per-domain-telemetry / telemetry-infrastructure | `NjordMetrics`, `njord_fetch_total`, enrichment metrics, `Njord.Ingest` meter, `ServiceDefaults`, `Akka.Logger.Serilog` | `*/Telemetry.cs`, `Njord.Budget/BudgetTelemetry.cs`, `CoreSetupContainer.cs:21-29`, `Njord/Configuration/AkkaSetupContainer.cs:48-53` |
| structured-logging / stream-log-tracing | `SchedulerActor` source context, Info-level poll summary and data-changed logs, log stages in every graph | `Njord.Pipeline/PollScheduler.cs:242,325` (Debug); only `Njord.Mqtt/MqttStatePublisher.cs:114,125` has `.Log` stages |
| message-response-naming / message-conventions / message-naming-convention / message-naming-consistency / message-domain-separation | `QueryNounResponse` contradicts the others; `NotAvailable` case unstated; stream-shutdown messages | `Njord.Messages/**` (e.g. `Budget/QueryBudgetUsage.cs`, `Forecast/QueryForecast.cs`) |
| budget-tracker-persistence / persistence-* | `BudgetTrackerActor`, `*Dto`, `Version`, `UtcTicks`, `[JsonProperty]`, Verify snapshots, forecast/enrichment DTO roundtrips, `SQLite` provider name | `Njord.Persistence/Events/**`, `Njord.Persistence.Tests/*.cs`, `AkkaSetupContainer.cs:41-46` (`SQLiteMS`) |
| actor-state-pattern / architecture-zone-enforcement / sensor-hub | `SchedulerActor` fields, Ingest/Egress/Enrichment zones, order-guard spec, `SensorSnapshotFound` | `Njord.Architecture.Tests/*.cs`, `Njord.Messages/Sensors/QuerySensorSnapshot.cs` |
| test-project-structure / test-project-extraction / test-cleanup / test-options-monitor / deterministic-test-infrastructure / xml-doc-cleanup | `Njord.IntegrationTests`, `FailingRefProvider`, `*DtoSerializationSpec`, old test-project list, `WeightedBudgetGate` | `src/Njord.slnx`, `src/Njord.Tests.Shared/`, `src/Njord.Persistence.Tests/` |
| docs-site / docs-restructure / architecture-diagrams / readme / config-builder / docker-compose-reference | discovery docs, history feature, `docs.yml` workflow, `architecture.svg`, `NjordOptionsValidator` budget formula, `EnrichmentOptions` | `.github/workflows/release.yml:44-53`, `docs/public/index.png`, `README.md:43` |
| e2e-skill / e2e-stack-verification / e2e-docker-stack / e2e-config-scenarios / e2e-grpc-coverage / e2e-test-plan / e2e-enrichment-validation | Windows path `D:\GIT\ha-njord\e2e\`, Mosquitto in stack, two subagents, six enrichments incl. history | `e2e/docker-compose.e2e.yml` (two services), `.claude/skills/e2e-test/SKILL.md:9,14` |

### Removed capabilities

| Capability | Why the subject is gone | Evidence |
|---|---|---|
| egress-event | no `EgressEvent`/`EgressActor`/`ModelStateActor`/`Njord.Egress` | `Njord.Messages/Pipeline/PipelineEvent.cs` |
| snapshot-actors, snapshot-sharding, shard-region-entities, read-priority-mailbox, snapshot-persistence-dtos | no snapshot actors, shard regions, mailbox or snapshot DTOs | all registrations use `WithSingleton`/`WithActors`; `Njord.Persistence/Events` has only budget and scheduler records |
| enrichment-actor, pipeline-actor | actors removed; work split into `OpenMeteoGateway` and `ForecastPipeline` | `Njord.OpenMeteo/OpenMeteoGateway.cs`, `Njord.Pipeline/ForecastPipeline.cs` |
| historical-learning | no history feature anywhere; `EnrichmentKind` has no history member | `Njord.Messages/Pipeline/EnrichmentKind.cs` |
| discovery-toggle | `MqttOptions` has no `DiscoveryEnabled`; no discovery payloads | `Njord.Core/Configuration/Mqtt/MqttOptions.cs` |
| domain-core-merge | no `Njord.Core/Analysis`, no Newtonsoft in Core | `Njord.Core/Njord.Core.csproj` |
| dissolve-host-tests | `Njord.Tests` is the intended host test project; no `Njord.IntegrationTests` | `src/Njord.slnx` |

### Untouched but suspicious

- `vertical-feature-libraries`: listed as stale, but the main spec already matches the code
  (restated by `2026-10-13-restructure-compute`); the unsynced delta of
  `2026-10-14-typed-pipeline-stream` is obsolete. No change.
- `mqtt-options-consistency`: the MQTT toggle is read with
  `configuration.GetSection(...).Get<MqttOptions>()` (`MqttSetupContainer.cs:24-26`,
  `CoreSetupContainer.cs:54-56`), a bound options instance but not `IOptions<MqttOptions>`.
  Left as is ("or an equivalent options-based path").
- `stream-supervision`, `actor-resolve-failure-handling`, `sensor-hub-state`,
  `metrics-naming-hygiene`, `index-preferences`, `kestrel-dual-port`: correct; the stale-name
  hits were removal statements or library types (`MissingActorRegistryEntryException`).
- `architecture-zone-enforcement` "Foundation layers have no framework dependency": only
  `Njord.Messages` is enforced by a test (`LayerReferenceSpec.cs:44`).

## Known code deviations (kept out of this change)

| # | Deviation | Evidence | Spec treatment |
|---|---|---|---|
| 1 | `ForecastReadModel` keeps only the last analysis event per location, so `GetEnrichments` returns at most one payload | `Njord.Pipeline/ForecastReadModel.cs:38,190` | `grpc-v2-weather-service` keeps "All enabled enrichments returned" |
| 2 | `StreamEnrichments` ignores the request's `location` filter | `Njord.Grpc/WeatherGrpcService.cs:91` | `grpc-v2-weather-service` keeps "matching the location filter" |
| 3 | `ConsensusOptions.Enabled` is not honored; `ConsensusComputed` is always emitted | `Njord.Pipeline/ForecastPipeline.cs:242`, `ConsensusSettings` has no toggle | `hourly-consensus` keeps the toggle requirement |
| 4 | Nothing sends `SensorSnapshot` to `ForecastPipeline`, so live indoor readings never reach `IndexAnalyzer` | `ForecastPipeline.cs:148`; `SensorHub` only answers queries | `pipeline-composition` describes the side channel as built |
| 5 | A corrupt `data/appsettings.Override.json` fails startup instead of falling back | `Njord/Program.cs:30` | `config-persistence` keeps "Corrupt config file falls back to defaults" |
| 6 | A poll cycle with a failed fetch never completes its summary/metrics | `PollScheduler.cs:247,261,296` (only on success) | `structured-logging` describes the success path |
| 7 | MQTTnet publish errors are not in the decider's resume list and stop the MQTT stream; no outage buffer | `Njord.Core/Actors/StreamSupervision.cs`, `MqttStatePublisher.cs:126-130` | `egress-stream-graph` buffer requirement removed |
| 8 | Several `Njord.Core/Configuration` files hold more than one type | `ConfigChangeMessages.cs`, `ConfigReloadPolicy.cs`, `IConfigWriter.cs`, `Polling/BudgetCalculator.cs` | `service-configuration` rule narrowed to options and validators |
| 9 | `NjordOptionsValidator` does not range-check latitude/longitude | `NjordOptionsValidator.cs:28-38` | `service-configuration` no longer claims it |
| 10 | No specs exist for `OpenMeteoGateway`, `GatewayBudgetGate` or `GatewayThrottleStage` | `src/Njord.OpenMeteo.Tests/` | none |
| 11 | Discovery leftovers: unused `TopicScheme.ConfigTopic`/`DeviceId`/`*UniqueId`, subscription to `{DiscoveryPrefix}/status` | `Njord.Mqtt/TopicScheme.cs:7-29`, `MqttConnection.cs:47,121` | specs describe them as they are |

## Risks / Trade-offs

- [The archive needs `--no-validate`] → run `openspec validate refresh-stale-specs` first;
  the delta structure is then already checked, and the manual folder deletion plus
  `openspec validate --all` closes the gap (see tasks).
- [Specs keep a few intended behaviors the code lacks (D1)] → they are listed above with
  evidence, so a later change can fix the code or drop the requirement.
- [Removed requirements of renamed capabilities carry a generic reason] → each points to
  the requirement that replaces it in the same capability.
- [E2E entity counts may still include a history sensor that ha-njord creates] → D6; to be
  rechecked against ha-njord when its repo is available.

## Migration Plan

1. Implement the documentation tasks and `launchSettings.json` (tasks 1–4).
2. Validate the change, archive it with `--no-validate`, delete the 12 emptied spec
   folders, rewrite the listed Purpose sections, validate all specs (tasks 5–6).
3. Build and run the full test loop (task 7). Rollback: revert the commit; no runtime
   behavior changes.
