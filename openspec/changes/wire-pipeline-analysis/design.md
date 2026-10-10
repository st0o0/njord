# Design — Wire Pipeline Analysis Output

## Decisions

### D1: IAnalysisStage in Core, implemented in Host

**Problem:** `PipelineActor` in `Njord.Pipeline` must invoke feature analyzers
(Alerts, Derived, Trends, Indices, History) but cannot reference their libraries
without breaking the layer architecture.

**Decision:** Define `IAnalysisStage` in `Njord.Core` with a single method:

```csharp
public interface IAnalysisStage
{
    Flow<ModelForecast, IReadOnlyList<PipelineEvent>, NotUsed> CreateFlow(IUntypedActorContext context);
}
```

The Host implements `AnalysisStage` in `Njord/Pipeline/AnalysisStage.cs`,
which has access to all feature libraries. `PipelineActor` receives it via DI
and calls `CreateFlow` during graph materialization.

**Why `IReadOnlyList<PipelineEvent>`:** Each `ModelForecast` produces multiple
events — the `ModelForecastUpdate` itself plus an `AnalysisResult` per enabled
feature. `SelectMany` flattens them into the `BroadcastHub`.

**Why pass `IUntypedActorContext`:** The history stage needs to `Ask` the
`ForecastHistoryActor` shard, which requires actor context to resolve the
shard region ref.

### D2: Analysis stage internal flow

```
ModelForecast
     │
     ▼
Scan(ModelSnapshot.Update)              ← accumulate all models per location
     │
     ▼
Select(snapshot → {
  consensus = factory.Create(snapshot, location)
  return (snapshot, consensus)
})
     │
     ▼
Scan((prev, curr) → {                  ← track previous consensus for trends
  pollCtx = new PollContext(curr.consensus, prev?.consensus, sensors,
                            curr.snapshot, location, horizons)
  return pollCtx
})
     │
     ▼
Select(pollCtx → {
  events = [ModelForecastUpdate]
  if (alerts.Enabled)  events += AnalysisResult("alerts", alerts.Compute(pollCtx))
  if (derived.Enabled) events += AnalysisResult("derived", derived.Compute(pollCtx))
  if (trends.Enabled)  events += AnalysisResult("trends", trends.Compute(pollCtx))
  if (indices.Enabled) events += AnalysisResult("indices", indices.Compute(pollCtx))
  return events
})
     │
     ▼
SelectAsync(pollCtx → {                ← history needs actor Ask
  record → query → analyze
  events += AnalysisResult("history", result)
  return events
})
```

Actually — simpler: combine all synchronous features in one `Select`, then
follow with a `SelectAsync` only for history. Or — if history is also disabled
or not worth the complexity of a separate stage — just do everything in a
single `SelectAsync` that Asks history and returns all events.

**Decision:** Single `SelectAsync` stage. All features run synchronously except
history's `ForecastHistoryActor` Ask. The `SelectAsync` parallelism is 1
(serialized per poll cycle). This is simpler than splitting the flow.

### D3: Eliminate ForecastSnapshotActor

**Problem:** `ForecastSnapshotActor` persists per-model forecasts for unary
`GetForecast`. But `GrpcConsumerActor` already receives `ModelForecastUpdate`
events and can hold them in memory.

**Decision:** Expand `GrpcConsumerActor.LatestResults` to hold a per-model
forecast dictionary instead of a single `LastForecast`. Change
`WeatherGrpcService.GetForecast` to query `GrpcConsumerActor` instead of
the snapshot shard.

Remove:
- `src/Njord.Grpc/ForecastSnapshotActor.cs`
- `src/Njord.Grpc/ForecastSnapshotMapping.cs`
- `src/Njord.Persistence/Grpc/ForecastSnapshotDto.cs`
- `src/Njord.Persistence/Grpc/ForecastPointDto.cs`
- `src/Njord.Persistence/Grpc/DailyForecastPointDto.cs`
- `src/Njord.Persistence/Grpc/ModelForecastDto.cs`
- `IForecastSnapshotRegion` from `src/Njord.Core/Actors/ActorKeys.cs`
- Shard registration from `src/Njord.Grpc/Configuration/GrpcSetupContainer.cs`
- Related test files

Add to `GrpcConsumerActor`:
- `QueryForecast` message handler (returns per-model forecast from state)
- Per-model forecast accumulation in `UpdateState`

### D4: SensorSnapshot injection into PollContext

The analysis stage needs `SensorSnapshot` for index computation (indoor temp
override). The `SensorHubActor` holds latest readings. The stage should Ask
`SensorHubActor` for the current snapshot as part of the `SelectAsync`.

Alternatively, `PipelineActor` could query sensors once per cycle and pass them
into the stage. But since we're already in a `SelectAsync`, one more Ask is
cheap.

**Decision:** The `AnalysisStage` resolves `ISensorHubActor` via context and
Asks for `SensorSnapshot` at each cycle inside the `SelectAsync`.

### D5: MQTT AnalysisResult mapping (existing TODO)

`MqttStateActor.MapToMqttMessages` has a TODO for `AnalysisResult` handling.
With analysis results now flowing, this needs to map each result type to its
MQTT state payload. The `StatePayloadBuilder` already exists and knows how to
format consensus, alerts, indices, etc. for MQTT.

**Decision:** Implement the `AnalysisResult` case in `MapToMqttMessages` by
switching on `TypeName` and delegating to `StatePayloadBuilder` methods. This
is a simple addition, not a new abstraction.

### D6: ModelCapability emission

`CapabilityTracker` was in `Njord.Egress` and is now deleted. The design said
capabilities should be extracted inline. For now, skip capability tracking —
it's not needed by any consumer and adds complexity. The `ModelCapability`
event type stays defined but is not emitted.

**Decision:** Defer capability emission. No consumer uses it yet.
