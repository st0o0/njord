# Design: convention-hygiene

## D1: NjordHealthState → NjordHealthMonitor with atomic snapshots

Replace the mutable multi-field class with immutable snapshot records
exchanged atomically:

```csharp
// Njord.Core/Health/NjordHealthMonitor.cs

public sealed record MqttHealthSnapshot(
    bool IsConnected,
    DateTimeOffset? DisconnectedSince);

public sealed record PollHealthSnapshot(
    DateTimeOffset? LastSuccessfulPollUtc);

public sealed record BudgetHealthSnapshot(
    long UsedDaily, long UsedMonthly,
    long LimitDaily, long LimitMonthly);

public sealed class NjordHealthMonitor
{
    private MqttHealthSnapshot _mqtt = new(false, null);
    private PollHealthSnapshot _poll = new(null);
    private BudgetHealthSnapshot _budget = new(0, 0, 0, 0);

    public DateTimeOffset ServiceStartedUtc { get; init; }

    public MqttHealthSnapshot Mqtt =>
        Volatile.Read(ref _mqtt);

    public PollHealthSnapshot Poll =>
        Volatile.Read(ref _poll);

    public BudgetHealthSnapshot Budget =>
        Volatile.Read(ref _budget);

    public void UpdateMqtt(MqttHealthSnapshot snapshot) =>
        Volatile.Write(ref _mqtt, snapshot);

    public void UpdatePoll(PollHealthSnapshot snapshot) =>
        Volatile.Write(ref _poll, snapshot);

    public void UpdateBudget(BudgetHealthSnapshot snapshot) =>
        Volatile.Write(ref _budget, snapshot);
}
```

Key change: each domain publishes its entire health slice as one atomic
reference write (`Volatile.Write` on a reference type is atomic on .NET).
Readers get a consistent snapshot — no torn reads.

Writers change from:
```csharp
// before (non-atomic)
_health.SetMqttConnected(utcNow);

// after (atomic)
_health.UpdateMqtt(new MqttHealthSnapshot(true, null));
```

Health checks change from property access to snapshot reads:
```csharp
var mqtt = _monitor.Mqtt;
if (!mqtt.IsConnected && mqtt.DisconnectedSince < threshold) ...
```

Budget fields are kept — they are read by `BudgetTelemetry.Observe()` for
Prometheus observable gauges.

## D2: DataChanged → PersistedDataChanged

Rename `Njord.Persistence.Events.Pipeline.DataChanged` to
`PersistedDataChanged`. The `[JsonProperty]` wire names stay unchanged —
existing journals deserialize correctly.

Update `PollSchedulerState` extension methods:
- `ToPersistedEvent(DataChanged evt)` returns `PersistedDataChanged`
- `FromPersistedEvent(PersistedDataChanged dto)` returns `DataChanged`

Remove the `using DataChanged = Njord.Messages.Pipeline.DataChanged;` alias
from `PollSchedulerState.cs` — no longer needed since the collision is gone.

## D3: IMqttConnection → IMqttConnectionActor

Rename in `Njord.Core/Actors/ActorKeys.cs`. Update host registration in
`AkkaSetupContainer`. Update all `RequiredActor<IMqttConnection>` references
to `RequiredActor<IMqttConnectionActor>`.

The transport interface `Njord.Mqtt.Transport.IMqttConnection` keeps its name
— it describes a connection, the actor key describes the actor that manages it.

## D4: TrendAnalyzer → TrendComputer

Rename the `static class TrendAnalyzer` in
`Njord.Compute/Analysis/TrendAnalyzer.cs` to `TrendComputer`. This is
consistent with the naming in the same namespace: `ConsensusComputer`,
`AlertComputer` are the compute-layer counterparts.

The DI-injected `Njord.Trends.TrendAnalyzer` keeps its name — it is the
analyzer that wraps the computer.

## D5: ForecastStreamFailed → RequestForecastStreamFailed

Rename in `Njord.Messages/Forecast/RequestForecastStream.cs`. This record is
the failure response to `RequestForecastStream`, matching the existing
`RequestForecastStreamResponse` (success path). Update all pattern matches
in `ForecastPipeline` and `OpenMeteoGateway`.

## D6: Snapshot interval → LastSequenceNr

Both actors switch from counter-based to `LastSequenceNr`-based scheduling:

```csharp
Persist(evt, _ =>
{
    _state = newState;
    if (LastSequenceNr % opts.SnapshotInterval == 0)
        SaveSnapshot(_state.GetPersistenceState());
});
```

- Remove `EventsSinceSnapshot` from `BudgetTrackerState` — it is a runtime
  scheduling concern, not domain state.
- Remove `_eventsSinceSnapshot` field from `PollScheduler`.
- Update `BudgetTrackerState.GetPersistenceState()` /
  `FromPersistence()` to drop the counter field (extend-only: the
  `[JsonProperty]` stays on the persisted record as nullable, old snapshots
  still deserialize, but new snapshots no longer write a meaningful value).

## D7: Apply methods take event records

Change signatures:

```csharp
// Before:
BudgetTrackerState.Apply(int weight, DateTimeOffset now)
PollSchedulerState.Apply(string key, int hash, DateTimeOffset utc, TimeSpan interval)

// After:
BudgetTrackerState.Apply(ApiCallRecorded evt, DateTimeOffset now)
PollSchedulerState.Apply(DataChanged evt, TimeSpan discoveryInterval)
```

The `now` parameter stays on BudgetTracker's Apply because the event
(`ApiCallRecorded`) doesn't carry the full timestamp — it stores `UtcTicks`
which needs conversion. Alternatively, make `Apply` extract from the event
record.

Recovery `ApplyRecover` methods continue to take persistence DTOs directly —
that's the recovery path and is correct.

## D8: Journal cleanup after snapshot

Add the FunkArr pattern to both persistent actors. Each gets a private
`MaybeSnapshot()` helper and journal cleanup on success:

```csharp
private void MaybeSnapshot()
{
    if (LastSequenceNr % SnapshotInterval == 0)
        SaveSnapshot(_state.GetPersistenceState());
}
```

Expand `SaveSnapshotSuccess` handler:

```csharp
Command<SaveSnapshotSuccess>(e =>
{
    _log.Debug("Snapshot saved at {SequenceNr}, cleaning journal",
        e.Metadata.SequenceNr);
    DeleteMessages(e.Metadata.SequenceNr);
    DeleteSnapshots(new SnapshotSelectionCriteria(e.Metadata.SequenceNr - 1));
});
Command<DeleteMessagesSuccess>(_ => { });
Command<DeleteMessagesFailure>(f =>
    _log.Warning(f.Cause, "Journal cleanup failed"));
Command<DeleteSnapshotsSuccess>(_ => { });
Command<DeleteSnapshotsFailure>(f =>
    _log.Warning(f.Cause, "Snapshot cleanup failed"));
```

This prevents unbounded journal/snapshot growth — without it, every event
since the first persist accumulates forever.
