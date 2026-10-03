## Context

See proposal.md for motivation. A read-only drift audit extracted backticked identifiers from all 88 main specs and checked them against `src/**/*.cs` and `protos/**/*.proto`: 1,269 distinct symbols, 118 missing (9 %), 44 specs with at least one missing symbol, about 14–16 with real drift (a lower bound: renamed behavior without a renamed symbol is invisible to the method). While fixing the worst specs by hand, reading the code found additional drift the script cannot see (the 80 %/100 % budget threshold, the `hourly_parameters` field name, the `QueryXxx` message family).

Constraints: the code is the source of truth; no code, proto or CI change; no new requirements; MQTT specs only get minimal corrections because MQTT is under review on Sunday 2026-10-04.

## Goals / Non-Goals

**Goals:**
- Every symbol and behavior named in the changed specs exists in the code today.
- No spec describes a service that no longer exists.
- Still-true behavior of the retired v1 specs is not lost.

**Non-Goals:** see proposal.md. Additionally: this change does not try to find behavior drift beyond what a symbol check and reading the touched requirements reveal; a full behavioral audit is not attempted.

## Decisions

### 1. Retire the v1 gRPC specs instead of patching their names

`protos/` has only `njord/v2` (`admin`, `common`, `ops`, `sensor`, `weather`); the v1 services `ConfigService` and `ForecastService` are gone. For each v1 spec I compared its requirements with the matching v2 spec and the code:

| v1 spec | v2 home | carried over (verified in code) | not carried over |
|---|---|---|---|
| `grpc-config-service` | `grpc-v2-admin-service` | nothing needed (`GetConfig`, `StreamConfig`, enrichment config, `BudgetProjection` already covered) | v1 RPC list |
| `config-mutation-api` | `grpc-v2-admin-service` | budget warning 80–100 % (`BudgetCalculator.Validate`) | RemoveLocation/UpdateLocation side effects ("SchedulerActor stops poll jobs", "snapshot actors clear data", "scheduler adjusts timer", "DiscoveryActor updates payloads"): no handler found (`SchedulerActor` and `DiscoveryActor` do not observe options changes; only `AdminGrpcService` and `Program.cs` use `OnChange`/reload); "parameter change warns about restart": that warning string does not exist in code; patch-style `UpdateLocation`: replaced by replace-all `SetLocations` |
| `trigger-targets-rpc` | `grpc-v2-ops-service` `GetTargets` | poll-state scenario (phase "steady"/"discovery") | Timestamp requirement (already in the v2 spec) |
| `trigger-poll-rpc` | `grpc-v2-ops-service` `TriggerPoll`; `poll-scheduler` | non-blocking trigger scenario; `TriggerImmediatePoll` requirement moved to `poll-scheduler` | v1 proto namespace requirement |
| `server-status-api` | `grpc-v2-ops-service` `GetStatus` | budget-tracker timeout returns zero usage plus warning; `active_enrichments` content | `process_start_utc` int64 and `ModelStatus` field-number requirement (v2 uses Timestamps, already in the v2 spec) |
| `grpc-forecast-service` | `grpc-v2-weather-service` | unknown model returns `NOT_FOUND` | "actor timeout returns `UNAVAILABLE`": the code lets the Ask timeout propagate, no mapping exists; `GetLocations`/`GetModels` (merged into `GetCatalog`) |
| `grpc-forecast-streaming` | `grpc-v2-weather-service` `StreamForecasts` | multi-client scenario, disconnect shuts the stream down (kill switch) | location-filter scenario (already in the v2 spec) |

Not carried over means: dropped from the specs because the code does not do it today. Behavior that a user still expects (for example runtime reconfiguration side effects) is a product question, not a spec question, and is left for a separate decision.

*Alternative:* rename symbols inside the v1 specs. Rejected: it would keep two contradictory descriptions of the same RPCs and keep v1 package names in the spec set.

### 2. Retired specs are removed with REMOVED deltas, then the directories are deleted

Each requirement of a retired spec gets a `REMOVED` entry with Reason and Migration. After sync a spec with zero requirements remains as a stub and fails `openspec validate` (this is how `publisher-protocol` and `daily-consensus-aggregation` became stubs), so a cleanup task deletes the emptied directories right after the sync. `openspec-hygiene` uses the same approach for its two stubs.

### 3. MODIFIED deltas restate the complete requirement

Every `MODIFIED` block is generated from the full current requirement text with explicit, individually asserted substitutions (script in the scratchpad during authoring; the blocks in `specs/` are the result), so no scenario is silently dropped. Renamed requirement headings use `RENAMED` followed by `MODIFIED` under the new name. Scenarios that can no longer hold are removed by omission and listed in the table above or in decision 5.

### 4. Corrections found by reading, not by the symbol audit

- `grpc-v2-admin-service`: reject above 100 % of the monthly budget, warn above 80 % (`BudgetCalculator.Validate` → `WithinBudget = usagePercent <= 100`, warning for `> 80 and <= 100`). The 80 % figure in `AGENTS.md` is the startup check, not the mutation check. The "empty list rejected without force" scenario had no `force` field to refer to (`SetLocationsRequest` has none).
- `grpc-enrichment-api`: `ConsensusUpdate` has `hourly_parameters` and `daily_parameters`; `daily_summaries` and `DailyConsensusSummary` no longer exist in `common.proto`.
- `poll-status-query` said `GetPollStates` is stashed until Ready while `poll-scheduler` said it is answered in all behaviors; the code answers it in all five behaviors (`WaitingForPipeline`, `WaitingForRefs`, `Connecting`, `WaitingForConnection`, `Ready`). The stash requirement is removed.
- `snapshot-actors`: queries reply with `ForecastFound`/`ForecastNotFound`/`EnrichmentFound`/`EnrichmentNotFound`/`AllEnrichmentsResult`, not `null`; the consumer behavior lives in `StreamConsumerActor`, whose subclass is `GrpcSnapshotConsumerActor`.

### 5. `sensor-hub`: closed enum wins (user decision)

`SensorKind` has two values in code and `AGENTS.md` calls it closed. The spec shrinks to those two. The Sum and Latest scenarios (they used `SolarPanelPower` and `HeatPumpFlowTemp`) are removed. `AggregationStrategy.Sum`/`Latest` and their code in `SensorHubActor` remain as unused enum values; deleting them is a code change for a later change.

### 6. MQTT specs: minimal corrections only

`mqtt-egress` and the serialization requirement of `historical-learning` get symbol renames only (`TopicScheme.EnrichmentDeviceId`/`EnrichmentTopic`/`EnrichmentSubTopic`, `EgressEvent.CapabilityLearned`, `StatePayloadBuilder.FromHistory`). The four "TopicScheme provides ... helpers" requirements keep their headings and their expected output strings. If MQTT is removed on Sunday, these edits are simply deleted with the specs; if not, they are already correct.

### 7. Audit false positives are left alone

Missing symbols that are not drift (no spec change): negative requirements ("SHALL NOT exist": `pipeline-actor` `FetchStage`, `service-configuration` `EffectiveBudget`/`ResolveModels`/`ServiceDefaults`/rename history, `index-preferences` `HeatingBaseTemp`/`CoolingBaseTemp`, `daily-forecast-typing` `ForecastRecorded`/`HistoryResponse`, `message-conventions` `GetPollStates`/`GetBudgetUsage`), external APIs (Akka TestKit `ExpectMsg`/`WaitUntil`, `Testcontainers`, `KillSwitch`, `TimeZoneInfo`, `IHostedService`, `ScheduleTellOnce`), camelCase JSON spellings of existing members (`lastHash`, `nextPollUtc`). The historical-learning `ForecastRecorded`/`HistoryResponse` hits were real and are fixed.

## Risks / Trade-offs

- [A dropped v1 behavior was actually wanted] → The "not carried over" column lists each dropped behavior with the reason; restoring one means adding a requirement to the v2 spec deliberately, in its own change.
- [Folded scenarios claim behavior that is not exactly true] → Each folded scenario was checked against `OpsGrpcService`, `WeatherGrpcService`, `AdminGrpcService`, `BudgetCalculator` and `SchedulerActor`; the tasks re-run the symbol check per spec.
- [Merge conflicts with other open changes on the same specs] → Checked: no other open change touches these capabilities (see proposal Impact). Changes archived earlier that touch the same specs (`zone-architecture-tests`, `akka-failure-hygiene`) do not overlap these capabilities.
- [Specs drift again] → Out of scope here; `openspec-hygiene` adds the "specs describe observable behavior" rule. The symbol check used here (task group 9) can be rerun locally at any time.
- [Message names change again in `extract-pipeline-egress-projects` etc.] → Those stages move files, not message names, and carry their own spec deltas.

## Migration Plan

Docs only. Apply: sync the deltas (`openspec-sync-specs`, or archive), delete the seven emptied spec directories, edit the two `## Purpose` paragraphs, validate. Rollback: `git revert`.

## Open Questions

- Which of the dropped v1 side effects (scheduler timer adjustment, discovery refresh on horizon change, restart warning for parameter changes) are still product requirements? Answerable later in a behavior review; does not change this change's tasks.
