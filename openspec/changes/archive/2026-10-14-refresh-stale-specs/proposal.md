## Why

The main specs in `openspec/specs/` have drifted far from the code. Several refactorings
(gateway/pipeline redesign, enrichment restructuring, typed pipeline stream, Messages
separation, compute restructuring) were archived without syncing their deltas, so 107 of
150 capabilities still name removed actors (`PipelineActor`, `EgressActor`,
`ModelStateActor`, `EnrichmentActor`, snapshot shard regions), removed projects
(`Njord.Ingest`, `Njord.Egress`, `Njord.Enrichment`, `Njord.Domain`, `Njord.History`),
removed features (MQTT Discovery, historical learning) or behavior the code no longer has.
Specs that contradict the code are worse than no specs: every new change starts from
wrong premises. This change restates the specs against the current code.

## What Changes

- Restate every stale requirement against the code (MODIFIED where the requirement and all
  its scenarios still hold in substance; REMOVED + ADDED under a new name where a scenario
  or the requirement name itself is obsolete). See `design.md` for the audit table.
- Remove 12 capabilities whose subject no longer exists: `discovery-toggle`,
  `dissolve-host-tests`, `domain-core-merge`, `egress-event`, `enrichment-actor`,
  `historical-learning`, `pipeline-actor`, `read-priority-mailbox`,
  `shard-region-entities`, `snapshot-actors`, `snapshot-persistence-dtos`,
  `snapshot-sharding` (every requirement REMOVED with reason and migration).
- Reconcile `message-response-naming` with the other naming specs: the `QueryNounResponse`
  rule is removed; `message-conventions`, `message-naming-convention` and
  `message-naming-consistency` now state the real scheme (`<Noun>Result` /
  `<Noun>Failed` or `<Noun>NotAvailable` for queries, `<VerbNoun>Completed` /
  `<VerbNoun>Failed` / `<VerbNoun>Rejected` for commands).
- Replace the Windows path in `e2e-skill` with the repo-relative `../ha-njord/e2e/`.
- Non-spec cleanup in the same change: add `src/Njord/Properties/launchSettings.json` so
  `dotnet run` really uses the Development environment; fix `docs/` pages, the LikeC4 model,
  the config builder data, `docker-compose.example.yml`, `appsettings.Example.json`, the E2E
  artifacts and two stale AGENTS.md lines that still describe removed features, types and
  projects; rewrite stale or `TBD` Purpose sections after the archive.

## Non-goals

- No production code change other than the new `launchSettings.json`. Behavior gaps found
  during the audit (see `design.md`, "Known code deviations") are not fixed here; they need
  their own changes.
- No new capabilities; the gateway and budget behavior lives in the existing
  `poll-pipeline`, `dynamic-budget-throttle` and `budget-tracker-persistence` specs.
- No wording or style edits to requirements that are factually correct.

**API-budget impact:** none. Polling, request weight and the Open-Meteo request count are
unchanged (documentation-only change).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

Removed entirely (all requirements REMOVED):
- `discovery-toggle`: MQTT Discovery and `DiscoveryEnabled` no longer exist.
- `dissolve-host-tests`: contradicts the host test project `Njord.Tests`; no `Njord.IntegrationTests`.
- `domain-core-merge`: no `Njord.Core/Analysis`; analysis lives in Compute and the feature libraries.
- `egress-event`: `EgressEvent`/`EgressActor`/`ModelStateActor` replaced by `PipelineEvent` in `ForecastPipeline`.
- `enrichment-actor`: `EnrichmentActor` removed; analysis runs in `ForecastPipeline`.
- `historical-learning`: no history feature.
- `pipeline-actor`: `PipelineActor` split into `OpenMeteoGateway` and `ForecastPipeline`.
- `read-priority-mailbox`: snapshot actors and their mailbox removed.
- `shard-region-entities`: no shard regions; all actors are singletons.
- `snapshot-actors`: forecast/enrichment snapshot actors removed; `ForecastReadModel` holds latest state.
- `snapshot-persistence-dtos`: forecast/enrichment snapshot DTOs removed.
- `snapshot-sharding`: no snapshot shards to fan out over.

Pipeline, gateway and streams:
- `pipeline-composition`: `ForecastPipeline` analysis stream, side channels, SourceRef output.
- `poll-pipeline`: `OpenMeteoGateway` fetch stream, feedback and forecast hub.
- `poll-scheduler`: `PollScheduler` with the gateway, `FetchForecastCompleted`/`Failed`, `Ack`.
- `pipeline-commands`: weight and `CycleId` assigned by the gateway.
- `poll-status-query`: messages in `Njord.Messages.Pipeline`, `PollStatesResult`.
- `failure-routing`: failures are transient with backoff for every reason.
- `dynamic-budget-throttle`: `GatewayThrottleStage`, `GatewayBudgetGate`, real burst size.
- `location-model-resolution`: inline union, `Continental` tier, coverage is a validation failure.
- `stream-composition`: per-actor materialization, metrics per fetch, typed handshake failures.
- `stream-consumer-actor`: startup goes through `WaitingForRefs`; `RequestPipelineOutput` ids.
- `producer-broadcast-pattern`: gateway, pipeline and read-model hubs.
- `consensus-snapshot`, `hourly-consensus`, `consensus-computation`: `ConsensusBuilder`/`ConsensusStatistics`, one consensus per forecast, real proto field numbers.
- `model-snapshot`, `weather-domain`, `utc-timestamps`, `openmeteo-client`: `Forecast` instead of `ModelForecast`, UTC requests, typed failures with location and model.

Consumers, MQTT and gRPC:
- `consumer-model`: typed `PipelineEvent` variants, `ForecastReadModel`, setup-container registration.
- `mqtt-actor-topology`, `egress-stream-graph`, `delta-publishing`, `optional-mqtt-egress`, `mqtt-egress`, `model-capability-tracking`: `MqttConnection`/`MqttStatePublisher`, no discovery, no capability learning, no outage buffer.
- `grpc-enrichment-api`, `grpc-v2-weather-service`, `grpc-v2-admin-service`, `grpc-v2-ops-service`, `grpc-v2-common`, `grpc-sensor-service`: read model and `ConfigManager` paths, reserved history fields, partial-status flags, two sensor kinds.

Analysis:
- `activity-indices`, `daily-slice-scoring`, `night-ventilation`: `IIndexScore` classes, `DaySlicer`, `IndexResult`, `hours_included`.
- `threshold-alerts`, `new-alert-types`, `severity-extensions`: `IAlertRule` classes on consensus medians, `AlertAnalyzer`.
- `derived-values`, `trend-analysis`: `DerivedFormulas`, `TrendFormulas`, `DerivedAnalyzer`.
- `daily-consensus`, `daily-forecast`, `daily-forecast-typing`, `enrichment-model-envelope`: discovery and history requirements removed; day-offset topics.
- `parameter-registry`, `parameter-typed-access`, `typed-constants`, `naming-collisions`, `structural-refactoring`: current type names and namespaces.

Configuration, hosting and health:
- `service-configuration`, `servus-bootstrap`, `domain-setup-containers`, `standalone-sub-options`, `writable-options`, `config-persistence`, `enrichment-config-validation`: setup containers per feature, own options sections, `IConfigWriter`, no history options.
- `health-checks`, `health-endpoint`: `NjordHealthMonitor`, Kestrel endpoint configuration, served routes.
- `prometheus-metrics`, `per-domain-telemetry`, `telemetry-infrastructure`, `structured-logging`, `stream-log-tracing`: per-domain meters and names, real log levels and log stages, no `ServiceDefaults`.

Messages, persistence, architecture and tests:
- `message-conventions`, `message-naming-convention`, `message-naming-consistency`, `message-response-naming`, `message-domain-separation`: one consistent naming scheme; no stream-shutdown messages.
- `budget-tracker-persistence`, `persistence-backoff`, `persistence-provider`, `persistence-record-hygiene`, `persistence-roundtrip-tests`, `persistence-funkarr-alignment`: `BudgetTracker`, positional `Persisted*` records, inline wire-format tests.
- `actor-state-pattern`, `architecture-zone-enforcement`, `sensor-hub`: current actors, feature-library rules, `SensorSnapshotResult`.
- `test-project-structure`, `test-project-extraction`, `test-cleanup`, `test-options-monitor`, `deterministic-test-infrastructure`, `xml-doc-cleanup`: current test projects and helpers.

Docs and E2E:
- `docs-site`, `docs-restructure`, `architecture-diagrams`, `readme`, `config-builder`, `docker-compose-reference`: no discovery or history, release-driven docs deploy, `index.png`.
- `e2e-skill`, `e2e-stack-verification`, `e2e-docker-stack`, `e2e-config-scenarios`, `e2e-grpc-coverage`, `e2e-test-plan`, `e2e-enrichment-validation`: repo-relative paths, no broker, three subagents, five analysis features.

## Impact

- `openspec/specs/`: 95 specs rewritten, 12 removed (after archive).
- `src/Njord/Properties/launchSettings.json`: new file (development launch profile).
- `docs/` (pages, LikeC4 model, config builder data and code), `docker-compose.example.yml`,
  `src/Njord/appsettings.Example.json`, `e2e/`, `.claude/skills/e2e-test/SKILL.md`,
  `AGENTS.md` and `openspec/config.yaml`: documentation fixes.
- No runtime behavior, API, persistence or wire-format change.
