---
name: njord-persistent-actor
description: Use when creating or modifying a persistent actor (ReceivePersistentActor) or a persistence DTO under src/Njord.Persistence. Covers snapshot-only vs event+snapshot actors, recovery, snapshot cleanup and the DTO/mapping shape.
---

# njord persistent actor

Mirrors what the code does today. Rules (extend-only DTOs, `TimeProvider`, Akka conventions) live in `AGENTS.md` — follow them, they are not repeated here.

## Pick the shape

| Need | Shape | Example |
|---|---|---|
| Derived/replaceable state, latest value wins | **Snapshot-only**: no events, `SaveSnapshot` every N updates | `src/Njord.Grpc/EnrichmentSnapshotActor.cs` |
| Accumulating state that must not lose increments | **Event + snapshot**: `Persist` each event, snapshot every N events | `src/Njord.Pipeline/BudgetTrackerActor.cs` |

Cleanup after `SaveSnapshotSuccess` differs: snapshot-only → `DeleteSnapshots` only; event+snapshot → `DeleteMessages` **and** `DeleteSnapshots`.

## Layout (per actor)

- `XxxActor.cs` — thin: message routing, persistence, lifecycle.
- `XxxState.cs` — immutable `sealed record` + `static class XxxStateExtensions` with `Apply(...)`, query helpers, `GetPersistenceState()` (state → DTO) and `FromPersistence(dto)` (DTO → state). See `src/Njord.Grpc/EnrichmentSnapshotState.cs`.
- `src/Njord.Persistence/XxxDtos.cs` — DTOs only (separate project `Njord.Persistence`, no references). The static `XxxMapping` classes live next to their consumer (`Njord.Pipeline`, `Njord.Grpc`, host `Enrichment/`), namespace `Njord.Persistence`.

## Actor skeleton (snapshot-only, from `EnrichmentSnapshotActor`)

```csharp
public sealed class EnrichmentSnapshotActor : ReceivePersistentActor
{
    private const int SnapshotInterval = 14;
    public override string PersistenceId => "enrichment-snapshot";
    private EnrichmentSnapshotState _state = EnrichmentSnapshotState.Empty;

    public EnrichmentSnapshotActor()
    {
        Recover<SnapshotOffer>(offer =>
        {
            if (offer.Snapshot is EnrichmentSnapshotDto saved)
                _state = EnrichmentSnapshotStateExtensions.FromPersistence(saved);
        });

        Command<UpdateEnrichment>(cmd =>
        {
            _state = _state.Apply(key, cmd.Result);
            if (_state.UpdatesSinceSnapshot >= SnapshotInterval)
            {
                SaveSnapshot(_state.GetPersistenceState());
                _state = _state.ResetSnapshotCounter();
            }
            Sender.Tell(new Ack(), Self);
        });

        Command<SaveSnapshotSuccess>(s => DeleteSnapshots(new SnapshotSelectionCriteria(s.Metadata.SequenceNr - 1)));
        Command<SaveSnapshotFailure>(f => _log.Warning("Snapshot save failed: {0}", f.Cause.Message));
        Command<DeleteSnapshotsSuccess>(_ => { });
        Command<DeleteSnapshotsFailure>(f => _log.Warning("Cleanup failed: {0}", f.Cause.Message));
    }
}
```

Event variant additions (`BudgetTrackerActor`):

- `Recover<XxxRecordedDto>` replays events into state; `Recover<SnapshotOffer>` restores the snapshot.
- Handler does `Persist(dto, _ => { _state = _state.Apply(...); /* snapshot at interval */ })` — update state **inside** the callback.
- `SaveSnapshotSuccess` → `DeleteMessages(seqNr)` + `DeleteSnapshots(new SnapshotSelectionCriteria(seqNr - 1))`.
- Register no-op handlers for `DeleteMessagesSuccess` / `DeleteSnapshotSuccess` so they are not unhandled.
- Fixed constants: `SnapshotInterval` (14–50 in the code today) and a stable string `PersistenceId`.
- Inject `TimeProvider`; query messages answer with `Sender.Tell(..., Self)`.

## DTO shape (from `src/Njord.Persistence/BudgetTrackerDtos.cs`)

- `sealed class` with settable props and Newtonsoft `[JsonProperty("short")]` names.
- `[JsonProperty("v")] public int Version { get; set; } = 1;` on every DTO.
- Timestamps stored as `long UtcTicks` (`"utc"`); rebuild with `new DateTimeOffset(ticks, TimeSpan.Zero)`.
- Static `XxxDtoMapping` / `XxxSnapshotMapping` with `ToDto` / `ToDomain` / `ToSnapshot`; no logic in the DTOs.
- Evolving a DTO: follow the "Persistence DTOs" rule in `AGENTS.md` (never rename/remove a `[JsonProperty]`, new props nullable or defaulted, bump `Version` on semantic change).
- Every DTO gets a wire-format spec with Verify (see `njord-actor-spec`, e.g. `src/Njord.Tests/Persistence/EnrichmentSnapshotDtoSerializationSpec.cs`).

## Checklist

1. Shape chosen from the table; `PersistenceId` unique across actors.
2. State record + extensions, DTO + mapping created.
3. Recover handlers cover snapshot (and events); cleanup handlers registered.
4. Wire-format Verify spec and a recovery spec (stop actor, recreate with same name, assert state).
