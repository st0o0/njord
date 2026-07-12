## Why

CycleId is created per-fetch in FetchStage instead of once per poll tick, so fetches within the same cycle carry different timestamps — contradicting the architecture guardrail "aggregate per poll cycle (cycle id = tick timestamp)." Additionally, the pipeline refactoring to MergeHub/BroadcastHub with SchedulerActor left behind dead code (ExpandStage, FetchStage as a standalone class) and unused config/fields (RetryBackoffMax, RetrievedAt).

## What Changes

- **CycleId placement**: Move CycleId creation from FetchStage into WeightedTarget so every fetch in a cycle shares the same timestamp. Remove CycleId parameter from `IOpenMeteoClient.FetchAsync` — the client reads it from the target.
- **StatePayloadBuilder anchor**: Replace `forecast.Cycle.Timestamp` with the forecast's actual retrieval time or an explicit "now" parameter — CycleId is a grouping key, not a time anchor for horizon computation.
- **Remove ExpandStage**: Dead code — the SchedulerActor now builds WeightedTargets directly.
- **Remove FetchStage**: Trivial indirection (one `SelectAsyncUnordered` + supervision). Inline into PipelineActor.
- **Remove `RetryBackoffMax`** from NjordOptions: Config property that nothing reads; ModelPollState hard-codes the same 15-min cap.
- **Remove `RetrievedAt`** from ModelForecast: Redundant with CycleId's timestamp, never read in production code.
- **Remove CycleId from `FetchOutcome.Failure`**: Produced but never consumed (failures are filtered out by `.Collect()`).
- **Remove ScaffoldSpec**: Placeholder test (`Assert.True(true)`) that has served its purpose.

## Non-goals

- Changing the adaptive scheduling logic in ModelPollState or SchedulerActor.
- Introducing consensus aggregation (deferred per project decisions).
- Changing the MQTT egress or discovery payload structure.
- Wiring `RetryBackoffMax` to ModelPollState — the hard-coded constant is correct; the unused config property is the problem.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `poll-pipeline`: WeightedTarget now carries CycleId; FetchStage is removed (fetch logic inlined in PipelineActor).
- `pipeline-commands`: WeightedTarget gains a CycleId field; expand logic assigns it at target-creation time.
- `openmeteo-client`: FetchAsync signature drops the CycleId parameter; the client reads cycle from the target or receives it differently.
- `weather-domain`: ModelForecast drops RetrievedAt; CycleId is retained as the authoritative poll-cycle identifier. FetchOutcome.Failure drops CycleId.
- `service-configuration`: RetryBackoffMax is removed from NjordOptions.
- `pipeline-actor`: Fetch flow is inlined (no FetchStage dependency).

## Impact

- **Domain**: `ModelForecast` loses `RetrievedAt` field; `FetchOutcome.Failure` loses `CycleId`, `Location`, `Model` fields (or simplifies to just reason + detail).
- **Ingest**: `IOpenMeteoClient.FetchAsync` signature changes (CycleId no longer a parameter — provided via WeightedTarget or constructed internally).
- **Pipeline**: `WeightedTarget` gains `CycleId`; `FetchStage.cs` and `ExpandStage.cs` deleted; PipelineActor inlines the fetch flow.
- **Egress**: `StatePayloadBuilder.Anchor` takes an explicit `DateTimeOffset` instead of reading from CycleId.
- **Config**: `NjordOptions.RetryBackoffMax` removed; `NjordOptionsValidator` updated.
- **Tests**: FetchStageSpec, ExpandStageSpec, CycleIdSpec, ScaffoldSpec, PollPipelineSpec all updated or removed. All other test files that construct ModelForecast or FetchOutcome updated for changed signatures.
