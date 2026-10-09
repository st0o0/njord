## Why

A cross-codebase review against the sibling FunkArr project found three categories of
logging issues in njord that FunkArr avoids entirely:
1. **Positional placeholders** (`{0}`, `{1}`) in `ForecastSnapshotActor.cs` (line 66),
   `EnrichmentSnapshotActor.cs` (lines 64-79), and `PipelineSetupContainer.cs` (lines 78-83)
   — these break structured logging (fields can't be queried by name in log aggregation
   systems like Seq/Loki). FunkArr uses named placeholders like `{TvdbId}` consistently.
2. **Excessive Info-level** for routine operations: `MqttDiscoveryActor.cs` logs "ready",
   "idle", "late capability", "capability expanded", "back online" at Info (7 log points);
   `EnrichmentActor.cs` logs "enrichment computed" at Info every cycle;
   `SchedulerActor.cs` logs model changes, initial polls, trigger results at Info. FunkArr
   reserves Info for startup/shutdown only; routine operations use Debug.
3. **Log-and-forget on timeouts**: `OpsGrpcService.cs` catches `AskTimeoutException` 3
   times (lines 54-56, 103-105, 177-179) and returns zero/empty data with only a warning —
   the gRPC caller cannot distinguish "actor timed out" from "legitimately no data". Snapshot
   actors log exception messages as plain text instead of passing exception objects for
   structured context.

## What Changes

- Replace all positional `{0}`/`{1}` placeholders with named placeholders across the three
  affected files.
- Demote routine-operation log calls from Info to Debug across `MqttDiscoveryActor`,
  `EnrichmentActor`, and `SchedulerActor`, keeping Info only for lifecycle events
  (startup, shutdown, config change) and genuinely noteworthy state changes.
- For `OpsGrpcService` timeout handling: either propagate the timeout as a gRPC status
  (e.g. `StatusCode.Unavailable` with a detail message), or add an explicit "partial"
  indicator to the response so callers can tell the data is incomplete — design decision
  in design.md.
- Pass exception objects to log methods instead of interpolating `ex.Message` as a plain
  string in snapshot actor failure handlers.

## Capabilities

### New Capabilities

- `structured-logging-hygiene`: named placeholders, appropriate log levels, and explicit
  partial-response indication on timeout — operator-facing quality.

### Modified Capabilities

None.

## Impact

- `src/Njord.Grpc/ForecastSnapshotActor.cs`, `src/Njord.Grpc/EnrichmentSnapshotActor.cs`
- `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`
- `src/Njord.Mqtt/MqttDiscoveryActor.cs`
- `src/Njord.Enrichment/EnrichmentActor.cs`
- `src/Njord.Pipeline/SchedulerActor.cs`
- `src/Njord.Grpc/OpsGrpcService.cs`
- No tests expected to break (log levels aren't asserted in specs, except where
  `OpsGrpcService`'s timeout behavior changes — those specs need updating).

## Non-goals

- Not adding new log statements — only fixing existing ones.
- Not changing Serilog configuration or sink setup.
- Not changing log output format (JSON vs text).

## API budget impact

None.
