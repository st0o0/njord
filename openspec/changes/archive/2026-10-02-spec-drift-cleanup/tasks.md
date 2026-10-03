## 1. Prerequisites and baseline

- [x] 1.1 Confirm no other open change edits these capabilities: `grep -rlE "^## (ADDED|MODIFIED|REMOVED|RENAMED)" openspec/changes/*/specs | grep -E "grpc-(config|enrichment|forecast|v2)|config-mutation-api|trigger-|server-status-api|poll-|snapshot-actors|budget-tracker|model-capability|historical-learning|sensor-hub|mqtt-egress"` lists only `openspec/changes/spec-drift-cleanup/...` (and, if `openspec-hygiene` is applied first, re-check that it did not touch `daily-forecast`/`config-builder` in a way that conflicts)
- [x] 1.2 Baseline: `openspec validate --all` and record which specs fail before this change (the six failures owned by `openspec-hygiene` are expected until that change is applied)

## 2. Retire the v1 gRPC specs (`openspec/specs/...`)

- [x] 2.1 Sync the REMOVED deltas for `grpc-config-service`, `config-mutation-api`, `trigger-targets-rpc`, `trigger-poll-rpc`, `server-status-api`, `grpc-forecast-service`, `grpc-forecast-streaming` (use `openspec-sync-specs` or archive)
- [x] 2.2 Delete the emptied spec directories `openspec/specs/{grpc-config-service,config-mutation-api,trigger-targets-rpc,trigger-poll-rpc,server-status-api,grpc-forecast-service,grpc-forecast-streaming}/` after `grep -rn "grpc-config-service\|config-mutation-api\|trigger-targets-rpc\|trigger-poll-rpc\|server-status-api\|grpc-forecast-service\|grpc-forecast-streaming" openspec/specs docs README.md` shows no remaining references (fix any hit that is a live reference)

## 3. gRPC v2 specs

- [x] 3.1 Sync the deltas for `grpc-v2-admin-service` (reject above 100 %, no `force`, budget warning scenario), `grpc-v2-ops-service` (`GetStatus`, `GetTargets`, `TriggerPoll` scenarios), `grpc-v2-weather-service` (`GetForecast` unknown model, `StreamForecasts` scenarios), `grpc-enrichment-api` (`WeatherService`, `hourly_parameters`, no `daily_summaries`)
- [x] 3.2 Re-verify each folded scenario against `src/Njord/Grpc/AdminGrpcService.cs`, `OpsGrpcService.cs`, `WeatherGrpcService.cs` and `src/Njord/Configuration/BudgetCalculator.cs` (budget reject `> 100`, warn `> 80 and <= 100`; budget-tracker timeout returns zero usage; phase strings `"steady"`/`"discovery"`; `NotFound` for unknown model; kill switch on cancellation)

## 4. Scheduler and polling specs

- [x] 4.1 Sync the deltas for `poll-status-query` (rename to `QueryPollStates query returns ...`, `PollStatesResult`, remove the "stashed until Ready" requirement) and `poll-scheduler` (union of global and location models, `QueryPollStates is handled in all behaviors`, added `TriggerImmediatePoll` requirement)
- [x] 4.2 Re-verify against `src/Njord/Pipeline/SchedulerActor.cs` (`WaitingForPipeline`/`WaitingForRefs`/`Connecting`/`WaitingForConnection`/`Ready` all handle `QueryPollStates`; `TriggerImmediatePoll` is stashed outside `Ready`; reply `TriggerPollResult`) and `SchedulerMessages.cs` (messages are sealed records in `Njord.Pipeline`)

## 5. Actor specs

- [x] 5.1 Sync the deltas for `snapshot-actors` (`QueryForecast`/`ForecastFound`/`ForecastNotFound`, `QueryEnrichment`, `QueryAllEnrichments`, renamed consumer requirement `GrpcSnapshotConsumerActor routes ...`) and `budget-tracker-persistence` (`QueryBudgetUsage`, `BudgetUsageResult`)
- [x] 5.2 Edit the `## Purpose` of `openspec/specs/snapshot-actors/spec.md`: replace "A `SnapshotConsumerActor` routes events ..." with "A `GrpcSnapshotConsumerActor` (built on `StreamConsumerActor`) routes events ..." and drop the sentence about replacing the removed in-memory `ForecastSnapshotStore`/`EnrichmentSnapshotStore`
- [x] 5.3 Re-verify against `src/Njord/Grpc/SnapshotMessages.cs`, `ForecastSnapshotActor.cs`, `EnrichmentSnapshotActor.cs`, `GrpcSnapshotConsumerActor.cs`, `src/Njord/Actors/StreamConsumerActor.cs` (`_watchedDeps`, `_lastTerminatedRef`, backoff `min(2^n, 30)` seconds, kill switch) and `src/Njord/Pipeline/BudgetTrackerActor.cs`; if the backoff in `StreamConsumerActor` differs from "min(1s × 2^retryCount, 30s)" correct the requirement text in the delta (the code currently uses `Math.Min(Math.Pow(2, _retryCount), 30)` seconds)

## 6. Sensor spec

- [x] 6.1 Sync the delta for `sensor-hub` (two `SensorKind` values, `QuerySensorSnapshot` with `SensorSnapshotFound`/`SensorSnapshotNotFound`, Sum/Latest scenarios removed, `SensorService.Push`/`StreamPush`)
- [x] 6.2 Re-verify against `src/Njord/Domain/Sensors/SensorKind.cs`, `SensorHubMessages.cs`, `src/Njord/Sensors/SensorHubActor.cs`, `src/Njord/Grpc/SensorGrpcService.cs`, `protos/njord/v2/sensor.proto`

## 7. MQTT-related specs (minimal corrections only)

- [x] 7.1 Sync the deltas for `mqtt-egress` (`EnrichmentDeviceId`/`EnrichmentTopic`/`EnrichmentSubTopic`, `EgressEvent.CapabilityLearned`), `model-capability-tracking` (two renamed requirements, horizon-capping text) and `historical-learning` (`ForecastRecordDto`, `HistoryOptions.SnapshotInterval`, `ForecastHistoryResult`, `StatePayloadBuilder.FromHistory`)
- [x] 7.2 Edit the `## Purpose` of `openspec/specs/model-capability-tracking/spec.md`: replace "emits ModelCapabilityLearned messages" with "emits `EgressEvent.CapabilityLearned` events"
- [x] 7.3 Re-verify against `src/Njord/Mqtt/TopicScheme.cs`, `src/Njord/Egress/EgressEvent.cs`, `src/Njord/Enrichment/ForecastHistoryActor.cs`, `ForecastHistoryMessages.cs`, `src/Njord/Configuration/HistoryOptions.cs` and `src/Njord/Mqtt/StatePayloadBuilder.cs`; do not add any new MQTT requirement (Sunday review pending)

## 8. Symbol check per edited spec

- [x] 8.1 For each edited spec `<name>` in {`grpc-v2-admin-service`, `grpc-v2-ops-service`, `grpc-v2-weather-service`, `grpc-enrichment-api`, `poll-status-query`, `poll-scheduler`, `snapshot-actors`, `budget-tracker-persistence`, `sensor-hub`, `mqtt-egress`, `model-capability-tracking`, `historical-learning`) run from the repo root: `f=openspec/specs/<name>/spec.md; grep -oE '`[A-Za-z_][A-Za-z0-9_.]*(\(\))?`' "$f" | tr -d '`()' | sed 's/.*\.//' | sort -u | while read -r s; do grep -rqw --include='*.cs' --include='*.proto' -- "$s" src protos || echo "MISSING: $s"; done`
- [x] 8.2 Every remaining `MISSING:` line must be a documented false positive (design decision 7: negative requirements such as `UtcNow`, external APIs); anything else is fixed in the spec before continuing

## 9. Validation

- [x] 9.1 `openspec validate spec-drift-cleanup` passes before the sync
- [x] 9.2 After the sync and the directory deletions: `openspec validate --all` shows no new failure compared with 1.2 (and no failure in the specs listed above)
- [x] 9.3 `git status --short` shows changes only under `openspec/`; nothing under `src/` or `protos/`
- [ ] 9.4 (skipped by the apply agent: no dotnet runs while another agent edits tests; no src/ change) No test suite is touched by this change; as a sanity check run from `src/`: `dotnet run --project Njord.Tests/Njord.Tests.csproj` (all green, no test count change)
- [x] 9.5 One Conventional Commit per task group, for example `docs(openspec): retire v1 gRPC specs` and `docs(openspec): align sensor-hub with closed SensorKind`; no push; no attribution trailers

## 10. Follow-up (not part of this change)

- [ ] 10.1 Decide which dropped v1 side effects (runtime reconfiguration of scheduler timer and discovery, restart warning for parameter changes) are still product requirements and, if so, add them as new requirements in a separate change
- [ ] 10.2 After the MQTT review on Sunday 2026-10-04, revisit `mqtt-egress`, `mqtt-actor-topology` and the MQTT-related specs; delete or rewrite them as decided
- [ ] 10.3 Optionally remove the unused `AggregationStrategy.Sum`/`Latest` code in `src/Njord/Domain/Sensors/SensorKind.cs` and `SensorHubActor.cs` (code change)

## Apply notes

- OpenSpec refused to archive with the seven fully retired specs (a spec with zero requirements is invalid), so their REMOVED deltas were set aside, the other twelve deltas were synced via `openspec archive`, the retired directories were deleted by hand (task 2.2) and the retirement deltas copied back into this archive for the record.
- Extra drift fixed while checking symbols (task 8.2): `_states` -> `SchedulerState.States` in `poll-status-query` and `poll-scheduler`.
- Task 5.3: the delta text `min(1s × 2^retryCount, 30s)` equals the code `Math.Min(Math.Pow(2, _retryCount), 30)` seconds, so no change.
- Remaining 8.1 hits are false positives: proto/gRPC keywords and status codes (`reserved`, `NOT_FOUND`), placeholders (`dN`, `hN`), camelCase JSON names of existing members, `ScheduleTellOnce` (Akka API) and negative requirements (`cooling_base_temp`, `heating_base_temp`, `UtcNow`, `ModelCapabilityLearned`).
