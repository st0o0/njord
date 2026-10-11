## Context

After three major refactors (`typed-pipeline-stream`, `wire-pipeline-analysis`,
`actor-architecture-cleanup`), a full codebase audit found 14+ dead code items,
5 unused NuGet packages, 4 stale AGENTS.md references, and 3 empty project
directories. All findings were verified via grep across the entire codebase.

## Goals / Non-Goals

**Goals:**
- Remove all verified dead code, types, files, and packages
- Correct stale documentation references
- Clean up PipelineActor (unused IWithStash, indentation, misplaced test)
- Remove unused Scheduler snapshot infrastructure

**Non-Goals:**
- Removing `*Failed` message variants (kept by convention, 1 line each)
- Adding PipeTo for `OfferAsync` (backpressure impossible at current rates)
- Updating archived OpenSpec specs

## Decisions

### 1. StreamsStopFailed → typed Ask

**Decision**: Remove `StreamsStopFailed` record; change `PipelineSetupContainer.StopStreamsOf`
from `Ask<object>` + type check to `Ask<StreamsStopped>`.

**Why**: PipelineActor's `StopStreams` handler is synchronous — it calls
`_killSwitch?.Shutdown()`, `_queue?.Complete()`, `_sensorRef?.Tell(...)` and
replies `StreamsStopped`. None of these can throw. If an unexpected exception
occurred, the outer `catch` in `StopStreamsOf` already handles it. The
`StreamsStopFailed` path is unreachable.

### 2. Remove Scheduler snapshot DTOs rather than keep for future

**Decision**: Delete `SchedulerSnapshotDto`, `ModelPollStateDto`,
`SchedulerDtoMapping.ToSnapshot`/`FromSnapshot`, and their tests.

**Why**: SchedulerActor explicitly ignores snapshots (`Recover<SnapshotOffer>(_ => { })`).
It event-sources `DataChangedDto` only. Adding snapshot support would be a
deliberate feature with its own spec. The event journal grows at ~1 event per
data change per model — at 8 models × 24 changes/day = ~192 events/day, which
is negligible for years. If snapshots become needed, the DTOs are trivial to
recreate from the domain types.

### 3. MqttConnectedSince removal

**Decision**: Remove the property, field, and setter logic. Keep
`SetMqttConnected` but have it only set `_isMqttConnected = 1` and clear
`_mqttDisconnectedSinceTicks`.

**Why**: Health checks read `IsMqttConnected` and `MqttDisconnectedSince` —
never `MqttConnectedSince`. The connected-since timestamp serves no consumer.

### 4. Keep *Failed message variants

**Decision**: Do not remove `QueryBudgetUsageFailed`, `QueryPollStatesFailed`,
`TriggerImmediatePollFailed`, `QueryForecastFailed`.

**Why**: The `message-response-naming` spec mandates the
Query → abstract Response → Result | Failed pattern. These variants cost 1 line
each and maintain consistency. When an actor adds `PipeTo` to a query handler,
the failure type is ready.

### 5. StreamSupervisionSpec placement

**Decision**: Move `StreamSupervisionSpec.cs` from `Njord.Pipeline.Tests` to
`Njord.Core.Tests`. It tests `StreamSupervision` which lives in `Njord.Core`.

**Why**: Test placement should match the project under test. The spec ended up
in Pipeline.Tests historically because the pipeline was the first consumer.

## Risks / Trade-offs

- **[Low] Test count reduction**: Removing 3 test classes (NjordMessageExtractorSpec,
  SchedulerDtoSerializationSpec, SchedulerDtoRoundtripSpec) reduces the total.
  All tested dead code, so no coverage loss on live code.
- **[Low] NuGet package re-add**: If a future feature needs Google.Protobuf or
  Testcontainers, `dotnet add package` re-adds them trivially.
