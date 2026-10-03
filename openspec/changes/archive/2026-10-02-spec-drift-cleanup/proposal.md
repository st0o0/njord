## Why

A read-only audit of the 88 main specs against `src/` and `protos/` found real drift in 14–16 specs (about 8 substantial). Several specs still describe the removed v1 gRPC services (`ConfigService`, `ForecastService`), actor messages that were renamed to the `QueryXxx`/`XxxResult` convention, and sensor kinds that no longer exist. Two v2 specs state behavior the code does not have (mutation rejected above 80 % of the budget; the code rejects above 100 % and warns between 80 % and 100 %). Specs that disagree with the code mislead agents and reviewers, which is exactly what the spec-driven workflow is meant to prevent.

## What Changes

- **Retire seven v1 gRPC specs** (all requirements REMOVED, directories deleted after sync): `grpc-config-service`, `config-mutation-api`, `trigger-targets-rpc`, `trigger-poll-rpc`, `server-status-api`, `grpc-forecast-service`, `grpc-forecast-streaming`. `protos/` contains only `njord/v2`; the v2 specs already cover the services.
- **Fold verified, still-true v1 behavior into the v2 specs** (MODIFIED with complete requirement text): budget warning between 80 % and 100 % (`grpc-v2-admin-service`), budget-tracker timeout and active-enrichment scenarios (`grpc-v2-ops-service` `GetStatus`), poll-state scenario (`GetTargets`), non-blocking trigger (`TriggerPoll`), unknown-model `NOT_FOUND` and multi-client/disconnect stream scenarios (`grpc-v2-weather-service`).
- **Correct wrong v2 behavior**: `grpc-v2-admin-service` rejects at 100 % (not 80 %) of the monthly budget and has no `force` flag.
- **Fix renamed or removed symbols** in `grpc-enrichment-api` (`WeatherService`, `hourly_parameters`, no `daily_summaries`), `poll-status-query` and `poll-scheduler` (`QueryPollStates`, `PollStatesResult`, union of global and location models; the contradicting "stashed until Ready" requirement is removed; the `TriggerImmediatePoll` requirement moves here from `trigger-poll-rpc`), `snapshot-actors` (`QueryForecast`/`ForecastFound`/`ForecastNotFound`, `QueryEnrichment`, `QueryAllEnrichments`, `GrpcSnapshotConsumerActor` on `StreamConsumerActor`), `budget-tracker-persistence` (`QueryBudgetUsage`, `BudgetUsageResult`), `historical-learning` (`ForecastRecordDto`, `ForecastHistoryResult`, `StatePayloadBuilder.FromHistory`), `model-capability-tracking` (`CapabilityLearned` requirement names).
- **`sensor-hub` follows the closed enum in code** (user decision): only `IndoorTemperature` and `IndoorHumidity`; the Sum and Latest scenarios that used removed kinds are dropped; `QuerySensorSnapshot`/`SensorSnapshotFound`/`SensorSnapshotNotFound` and `SensorService.Push`/`StreamPush` replace the old names.
- **MQTT specs get minimal symbol corrections only** (`mqtt-egress`: `EnrichmentDeviceId`/`EnrichmentTopic`/`EnrichmentSubTopic`, `EgressEvent.CapabilityLearned`) because MQTT will be reviewed on Sunday 2026-10-04 and may be removed.
- Direct edits of two `## Purpose` paragraphs (deltas cannot change them).

## Capabilities

### New Capabilities

None. No requirement is invented; every changed requirement restates behavior that exists in the code today.

### Modified Capabilities

- `grpc-v2-admin-service`: budget rejection at 100 %, no `force`, budget warning scenario.
- `grpc-v2-ops-service`: carried-over `GetStatus`, `GetTargets`, `TriggerPoll` scenarios.
- `grpc-v2-weather-service`: unknown-model and stream scenarios.
- `grpc-enrichment-api`: `WeatherService` names, `hourly_parameters`, no `daily_summaries`.
- `poll-status-query`, `poll-scheduler`: `QueryPollStates`/`PollStatesResult`, model resolution, `TriggerImmediatePoll` (moved).
- `snapshot-actors`, `budget-tracker-persistence`, `model-capability-tracking`, `historical-learning`, `sensor-hub`, `mqtt-egress`: renamed symbols and (for `sensor-hub`) the closed `SensorKind` list.
- Retired (all requirements removed): `grpc-config-service`, `config-mutation-api`, `trigger-targets-rpc`, `trigger-poll-rpc`, `server-status-api`, `grpc-forecast-service`, `grpc-forecast-streaming`.

## Impact

- Specs only: the delta files in this change and, after sync, `openspec/specs/**`. No code under `src/` changes and no proto changes.
- Overlap check: `openspec-hygiene` repairs/deletes `publisher-protocol`, `daily-consensus-aggregation`, `prometheus-metrics`, `structural-refactoring`, `config-builder`, `daily-forecast`; none of them appears here. The delta specs of `extract-pipeline-egress-projects` touch `enrichment-feature-registry`, `egress-event`, `enrichment-model-envelope`, `activity-indices`, `mqtt-enrichment-presentation`, `architecture-zone-enforcement`; none appears here either.
- API budget: none, no polling added or altered (0 requests/month against the 300k free-tier limit).

## Non-goals

- No code or proto changes, no CI, no new requirements, no MQTT redesign (the `mqtt-egress` and `historical-learning` serialization requirements only get symbol corrections).
- Not consolidating overlapping specs (consensus family, `daily-forecast-*`, `health-*`) and not changing wording style (`openspec-hygiene` adds the "specs describe observable behavior" rule).
- Not deleting the now-unused `Sum`/`Latest` aggregation code in `SensorHubActor` (a code decision for a later change).
- Not documenting behavior the code has but no spec mentions (for example the `Unavailable` gRPC status when the egress source cannot be created).
