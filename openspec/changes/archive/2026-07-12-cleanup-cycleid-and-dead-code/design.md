## Context

The pipeline was refactored from a monolithic Source.Queue graph to MergeHub/BroadcastHub with StreamRefs. During that refactoring, the SchedulerActor absorbed the expand logic (mapping commands to WeightedTargets), but `ExpandStage` was left behind. `FetchStage` remained as a standalone class wrapping a single `SelectAsyncUnordered`, and CycleId creation stayed inside it — meaning each fetch gets its own timestamp rather than sharing one per poll tick. Additionally, `ModelForecast.RetrievedAt` duplicates CycleId's timestamp, `FetchOutcome.Failure` carries a CycleId that's never consumed, `NjordOptions.RetryBackoffMax` is configured but never read, and `ScaffoldSpec` is a placeholder.

## Goals / Non-Goals

**Goals:**
- Make CycleId semantically correct: one timestamp per poll tick, shared by all fetches in that cycle
- Remove dead code that creates confusion about what's active
- Simplify the pipeline graph by inlining the trivial fetch flow
- Reduce ModelForecast to its essential fields

**Non-Goals:**
- Changing adaptive scheduling logic (ModelPollState, SchedulerActor timing)
- Introducing cycle-based aggregation or consensus
- Changing MQTT topics, discovery payloads, or egress structure
- Adding new features — this is purely cleanup

## Decisions

### D1: CycleId travels on WeightedTarget, not created at fetch time

**Choice**: Add `CycleId` to `WeightedTarget`. The SchedulerActor creates it when offering a target (from `_timeProvider.GetUtcNow()`). All targets produced by a single timer fire or manual refresh share the same CycleId.

**Alternative considered**: Pass CycleId as a separate parameter through the stream. Rejected — WeightedTarget is the natural carrier since it already represents "what to fetch in this cycle."

**Consequence**: `IOpenMeteoClient.FetchAsync` no longer receives CycleId as a parameter — it's part of the `WeightedTarget`. The client constructs `ModelForecast` using `target.Cycle`. The `CycleId.From(TimeProvider)` factory method can be removed since the scheduler creates CycleId directly.

### D2: StatePayloadBuilder.Anchor takes an explicit DateTimeOffset

**Choice**: Change `Anchor` and `BuildPerHorizon` to take a `DateTimeOffset anchorTime` parameter instead of reading `forecast.Cycle.Timestamp`. The caller (MqttEgressActor's consumer graph) passes `TimeProvider.GetUtcNow()` — "the current time when publishing."

**Rationale**: The horizon anchor ("what is +3h from now?") is a presentation concern at publish time, not a property of the poll cycle. CycleId answers "when was this data collected?" — a different question.

**Alternative considered**: Use `forecast.Cycle.Timestamp` as before but document it clearly. Rejected — the semantics are genuinely wrong: a cycle from 10 minutes ago would shift all horizons by 10 minutes.

### D3: Remove RetrievedAt from ModelForecast

**Choice**: Drop `RetrievedAt`. The only timestamps on a forecast are `CycleId` (when the poll tick fired) and each point's `ValidAt`.

**Rationale**: `RetrievedAt` is never read in production code. It was `timeProvider.GetUtcNow()` captured after the HTTP call, differing from CycleId by only HTTP latency. If actual fetch timing is needed later, it belongs in telemetry/tracing, not the domain model.

### D4: Simplify FetchOutcome.Failure

**Choice**: Remove `CycleId`, `Location`, and `Model` from `FetchOutcome.Failure`. Failures carry only `Reason` and `Detail`. The pipeline filters failures out via `.Collect()` — they're never consumed downstream.

**Rationale**: The failure context (which location/model failed) is already logged by the client. Carrying it in the outcome type implies someone routes on it, but nobody does.

### D5: Inline FetchStage into PipelineActor

**Choice**: Delete `FetchStage.cs`. The `SelectAsyncUnordered` + supervision strategy moves directly into `PipelineActor.MaterializePipeline()`.

**Rationale**: FetchStage is a static method returning a single flow operator. The "stage" abstraction adds a file and an indirection but no encapsulation — the parallelism, client, and time provider are all passed through.

### D6: Delete ExpandStage

**Choice**: Delete `ExpandStage.cs`. Its logic (mapping commands to WeightedTargets with weight computation and validation) now lives in the SchedulerActor's `OnScheduledPoll`, `OnRefreshModel`, and `OnRefreshLocation` handlers.

### D7: Remove RetryBackoffMax from NjordOptions

**Choice**: Delete the property. `ModelPollState` keeps its hard-coded `MaxRetryBackoff = TimeSpan.FromMinutes(15)`. If this needs to be configurable in the future, it can be added then with proper wiring.

## Risks / Trade-offs

- **[Signature churn in tests]** → Many test files construct `ModelForecast` or `FetchOutcome.Failure` and will need signature updates. This is mechanical and low-risk. All changes are compile-error-driven — the compiler catches every callsite.
- **[CycleId granularity for manual refreshes]** → A `RefreshLocation` creates one CycleId for all models in that location. A `RefreshModel` creates one for a single model. Both are correct — the CycleId represents "this batch of work triggered at this moment." → No mitigation needed.
- **[Anchor time drift]** → Using `TimeProvider.GetUtcNow()` at publish time means the anchor moves slightly depending on when the egress consumer processes each forecast. In practice, `Anchor` rounds to the next full hour, so sub-minute drift has no effect. → Acceptable.
