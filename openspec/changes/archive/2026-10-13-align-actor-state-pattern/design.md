## Context

njord has 6 production actors. Two persistent actors (BudgetTracker, PollScheduler) already have state records but deviate from the FunkArr canonical form. One non-persistent actor (SensorHub) has domain state inline. Three stream/connection actors (ForecastPipeline, OpenMeteoGateway, MqttConnection) have infrastructure state only but are inconsistent in how they organize fields and lifecycle phases.

The FunkArr actor-state pattern (documented in `akka-skills:actor-state`) prescribes: state record in a separate file, `Empty` as a static readonly field, `FromPersistence` as a static method on the record, `ProcessCommand` tuple pattern, and actors as thin shells.

## Goals / Non-Goals

**Goals:**
- Every persistent actor follows the FunkArr pattern exactly (Empty field, FromPersistence on record, ProcessCommand)
- SensorHub domain logic is unit-testable without Akka
- All actors have consistent, predictable field ordering and lifecycle organization
- PollScheduler gets real snapshot persistence (currently a no-op)

**Non-Goals:**
- Changing actor behavior or message protocols
- Forcing State records on stream actors (ForecastPipeline, OpenMeteoGateway, MqttConnection)
- Extracting a shared base class for the gateway-resolve retry pattern
- Changing persistence wire format or `[JsonProperty]` strings

## Decisions

### D1: BudgetTrackerState.Empty becomes a static readonly field

**Choice:** `public static readonly BudgetTrackerState Empty = new(0, 0, 0, 0, 0)` — month/day fields start at 0, and the actor's constructor applies the current time via an initialization step.

**Why not keep the `Empty(TimeProvider)` method?** The FunkArr pattern requires `Empty` to be a static readonly field with compile-time defaults. The current approach couples state initialization to TimeProvider, which makes the state record harder to use in tests and breaks the canonical pattern. The actor already handles month/day boundary resets in `Apply`, so starting from zeroed values and letting the first event or snapshot recovery set the correct month/day is safe.

**Migration:** The `Apply` and `ApplyRecover` methods already handle month/day boundary logic, so recovery replays or snapshot restoration will set the correct values regardless of the initial state.

### D2: FromPersistence moves from extension to static method on record

**Choice:** Move `FromPersistence` from `BudgetTrackerStateExtensions` to `BudgetTrackerState` as a static method. Same for `PollSchedulerState`.

**Why?** `FromPersistence` is a factory — it constructs a new state from external data. Factories belong on the type, not on an extensions class. The FunkArr pattern places it alongside `Empty` on the record. Extension methods are for instance operations (`Apply`, `GetSnapshot`, `GetPersistenceState`).

### D3: ProcessCommand tuple pattern for BudgetTracker

**Choice:** Add `ProcessCommand(RecordApiCall cmd, DateTimeOffset now)` returning `(BudgetTrackerState State, ApiCallRecorded Event)`.

**Why?** The current code constructs the event in the actor via `ToPersistedEvent`, then calls `Apply` separately in the persist callback. The ProcessCommand pattern unifies this: one call produces both the new state and the event to persist, keeping the actor handler to a single destructure + Persist call.

### D4: PollScheduler gets real snapshot persistence

**Choice:** Add `GetPersistenceState()` and `FromPersistence()` to PollSchedulerState. The `Recover<SnapshotOffer>` handler currently does nothing — it will call `FromPersistence` to restore state.

**Why?** Without snapshot recovery, PollScheduler replays all journal events on every restart. For a long-running service this grows unbounded. The existing `PersistedPollSchedulerState` DTO in `Njord.Persistence` may need to be created or extended.

**Wire format:** A new `PersistedPollSchedulerState` DTO in `Njord.Persistence.Events.Pipeline` with the model poll states dictionary. Follows extend-only conventions.

### D5: SensorHub state extraction

**Choice:** Extract `_readings` dictionary, validation, expiry, and aggregation into `SensorHubState` + `SensorHubStateExtensions` in `Njord.SensorHub/`.

**State shape:** `SensorHubState(ImmutableDictionary<(string Location, SensorKind Kind, string Source), SensorReading> Readings)` with `Empty` as a static readonly field.

**Extension methods:**
- `Apply(SensorReading)` — stores/overwrites a reading
- `Validate(SensorReading)` — checks plausibility, returns result
- `GetSnapshot(string location, DateTimeOffset now, TimeSpan staleness)` — filters, aggregates, returns snapshot
- `Expire(DateTimeOffset now, TimeSpan staleness)` — returns state without stale readings

**Actor becomes:** Receive handlers that call validate → apply → tell, and a timer handler that calls expire.

### D6: Stream actor cleanup — convention, not abstraction

**Choice:** Apply consistent field ordering and lifecycle organization to ForecastPipeline, OpenMeteoGateway, and MqttConnection without introducing State records or shared base classes.

**Field order convention:**
1. Constants and static readonly fields (policies, etc.)
2. Private nested message records
3. Injected dependencies (readonly)
4. Runtime/mutable refs (materializer, queues, actor refs)
5. Logger (`_log`)
6. State record (if any)

**Lifecycle convention:** Become-phase methods are named after the phase. Stream materialization lives in a dedicated method.

## Risks / Trade-offs

- **[BudgetTracker Empty(0,0,0,0,0)]** Month/day start at 0 — the first `Apply` or `FromPersistence` sets real values. Risk: code that reads month/day before any event is applied sees 0. → Mitigation: Only the actor touches state, and it always recovers before processing commands. Tests construct via `Empty` + `Apply` for the same reason.

- **[PollScheduler snapshot DTO]** Introducing a new persistence DTO means the first snapshot after upgrade has no prior snapshots to recover from. → Mitigation: Event replay still works as before. Snapshots are an optimization, not a requirement. The first snapshot saves after the configured interval.

- **[SensorHub ImmutableDictionary]** Switching from `Dictionary` to `ImmutableDictionary` has a small performance cost per write. → Mitigation: SensorHub processes at most a handful of readings per minute. The immutability benefit (testability, safety) far outweighs the cost.

## Open Questions

None — all decisions are straightforward refactoring with no behavioral changes.
