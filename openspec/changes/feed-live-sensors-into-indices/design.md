## Context

`ForecastPipeline.MaterializeAnalysisPipeline` zips each forecast with two side channels, `SensorSnapshot` and `AnalysisSettings`. Each is a `Source.ActorRef` with an initial element and `RepeatPrevious`. The sensor channel starts with an empty snapshot and is fed only by `Receive<SensorSnapshot>` in the ready behavior, and no actor sends that message. The SensorHub (`ISensorHub`, cluster singleton, `Njord.SensorHub`) keys readings by `(Location string, SensorKind, Source)` with ordinal location matching. It answers `QuerySensorSnapshot(LocationId)` with `SensorSnapshotResult` or `SensorSnapshotNotAvailable`, and it applies the staleness window at query time. `SensorProtoMappings` validates the location case-insensitively against the catalog, but it forwards `request.Location` as sent.

## Goals / Non-Goals

**Goals:**
- Indices of a location use that location's fresh sensor readings.
- Sensor problems never stall or drop analysis.

**Non-Goals:**
- Recomputing indices when a reading arrives.
- Changing SensorHub internals (keys, aggregation, expiry).

## Decisions

### D1: Pull per forecast with `SelectAsync(1)` instead of a pushed side channel

A `SelectAsync(1, ...)` stage before the stateful analysis stage turns each forecast into `(forecast, SensorSnapshot?)` by asking the SensorHub for `QuerySensorSnapshot(new LocationId(forecast.Location))`. `SelectAsync(1)` keeps arrival order and analyzes one forecast at a time, as today. The settings side channel stays, so the zip shrinks to forecast plus settings.

*Alternatives:*
- SensorHub pushes snapshots to the pipeline: this needs a snapshot per location, extra expiry pushes, and the SensorHub would have to know the pipeline. The pull matches the AGENTS.md guardrail ("Analysis features pull sensor data at each poll cycle").
- Read the sensors inside `IndexAnalyzer`: that would put actor access into a pure feature library.

### D2: Resolve the SensorHub per lookup through the actor registry, with a short timeout

Inside the stage, the SensorHub is looked up per forecast from the `ActorRegistry` of the actor system: `TryGet<ISensorHub>`, captured when the graph is materialized, never `Context`. A miss yields `null`. This avoids a startup ordering dependency and the re-resolve logic a cached ref would need. The ask uses a named timeout of 2 s. A timeout or any exception is caught inside the async lambda and yields `null`, logged at Debug for not available and Warning for a timeout or exception. The stage itself never fails, so the existing supervision decider is not involved. This is `async`/`await` inside a stream stage, not actor code, so the no-`ContinueWith` and `PipeTo` rules do not apply.

`IndexAnalyzer` keeps receiving `null` for "no data"; the old empty-snapshot convention ("Readings.Count > 0") goes away together with the side channel.

### D3: Canonical location name at the gRPC boundary

`SensorProtoMappings` already finds the configured location case-insensitively. It now forwards that location's configured `Id.Name` instead of `request.Location`. The SensorHub stays ordinal, and its keys always use the same spelling as `Forecast.Location`. One fix at the single entry point is preferable to changing SensorHub comparisons.

### D4: Tests

- **Pipeline** (`src/Njord.Pipeline.Tests/`): a spec registers a test `ISensorHub` actor that answers per location; it drives forecasts through the pipeline the way existing pipeline specs do and asserts on the emitted `IndexComputed` events. The cases:
  - a live temperature changes the night-ventilation result compared to the configured value;
  - the location mismatch falls back;
  - with no SensorHub registered, all events are still emitted;
  - with a SensorHub that never replies, all events are emitted within the timeout plus margin.
- **Staleness:** already covered by the SensorHub specs; the pipeline only relies on `SensorSnapshotNotAvailable`.
- **gRPC** (`src/Njord.Grpc.Tests/`): the mapping forwards the configured spelling.

## Risks / Trade-offs

- [Each forecast waits for one in-process ask] → Normally sub-millisecond. The worst case is the 2 s timeout when the SensorHub singleton is down, and the pipeline is not latency-critical (hourly polls).
- [Timeout during singleton hand-over at startup] → Forecasts that arrive in that window use the configured values, which is the same result as today.

## Migration Plan

There is no config, wire-format or persistence change. Roll back by reverting the commit.
