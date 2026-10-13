# Convention Hygiene — HealthState redesign, name collisions, persistence FunkArr alignment

## Problem

A full convention audit against AGENTS.md and FunkArr standards uncovered five
findings. Actor conventions, test conventions, and persistence records are
clean — the issues are concentrated in shared infrastructure and naming.

### Finding 1: NjordHealthState (High)

`NjordHealthState` is a mutable `class` with 7 `Interlocked` fields shared
across 3 actors (`MqttConnection`, `PollScheduler`, `BudgetTracker`) as a DI
singleton. It violates multiple conventions:

- **`*State` suffix** on a mutable class (suffix reserved for immutable state
  records)
- **Shared mutable state** bypasses actor message-passing discipline
- **Non-atomic MQTT writes** — two `Interlocked.Exchange` calls for connected +
  disconnectedSince; a reader between them sees inconsistent state
- **Unused parameter** — `SetMqttConnected(DateTimeOffset utcNow)` ignores
  `utcNow`

### Finding 2: DataChanged name collision (High)

`Njord.Messages.Pipeline.DataChanged` and
`Njord.Persistence.Events.Pipeline.DataChanged` collide. Both are in Core's
transitive closure — 11+ projects can hit CS0104. Currently worked around with
a `using` alias in `PollSchedulerState.cs`.

### Finding 3: IMqttConnection name collision (Medium)

`Njord.Core.Actors.IMqttConnection` (empty actor-key marker) collides with
`Njord.Mqtt.Transport.IMqttConnection` (real transport interface). Confusing
and latently ambiguous.

### Finding 4: TrendAnalyzer name collision (Medium)

`Njord.Compute.Analysis.TrendAnalyzer` (static pure-function class) collides
with `Njord.Trends.TrendAnalyzer` (DI-injected analyzer). Completely different
roles, same name.

### Finding 5: ForecastStreamFailed naming (Low)

`ForecastStreamFailed` breaks the `Request*Failed` response-naming pattern
established by `RequestForecastStream` → `RequestForecastStreamResponse`.

### Finding 6: Snapshot interval via counter instead of LastSequenceNr (Medium)

Both persistent actors use an explicit counter (`EventsSinceSnapshot` /
`_eventsSinceSnapshot`) instead of FunkArr's `LastSequenceNr % interval`.
BudgetTracker's counter lives on the domain state record — a runtime
scheduling concern that pollutes the immutable state.

### Finding 7: Apply takes decomposed values instead of event records (Low)

Both `BudgetTrackerState.Apply(int weight, DateTimeOffset now)` and
`PollSchedulerState.Apply(key, hash, utc, interval)` take decomposed
primitives. FunkArr expects `Apply(SomethingHappened evt)` taking the event
record directly.

## Proposed changes

### HealthState → NjordHealthMonitor with atomic state

Replace `NjordHealthState` (mutable class) with `NjordHealthMonitor` using an
immutable snapshot approach. Each domain publishes its health slice as an
immutable record via `Interlocked.Exchange` on a single reference — no
multi-field races. The `*State` suffix is dropped.

Rename to `NjordHealthMonitor` and restructure:
- One `MqttHealthSnapshot` record (connected + disconnectedSince together)
- One `PollHealthSnapshot` record (lastSuccessfulPoll)
- Budget fields stay (they ARE read by Prometheus gauges) but become a
  `BudgetHealthSnapshot` record
- Each actor exchanges one atomic reference, not multiple `long` fields

This keeps the shared-singleton approach (pragmatic for health checks) but
makes reads atomic and the API type-safe.

### DataChanged → PersistedDataChanged

Rename `Njord.Persistence.Events.Pipeline.DataChanged` to
`PersistedDataChanged`. Update `[JsonProperty]` wire name stays
unchanged (extend-only). Update `PollSchedulerState` conversion methods. Remove
the `using` alias workaround.

### IMqttConnection → IMqttConnectionActor

Rename the actor-key marker interface in `Njord.Core/Actors/ActorKeys.cs` from
`IMqttConnection` to `IMqttConnectionActor`. Update the host registration.

### TrendAnalyzer → TrendComputer

Rename `Njord.Compute.Analysis.TrendAnalyzer` (static class) to
`TrendComputer`. Consistent with `ConsensusComputer`, `AlertComputer` naming
in the same namespace.

### ForecastStreamFailed → RequestForecastStreamFailed

Rename in `Njord.Messages/Forecast/RequestForecastStream.cs` and all consumers.

### Snapshot interval → LastSequenceNr

Replace counter-based snapshot scheduling in both actors with
`LastSequenceNr % SnapshotInterval == 0`. Remove `EventsSinceSnapshot` from
`BudgetTrackerState` (runtime concern). Remove `_eventsSinceSnapshot` field
from `PollScheduler`.

### Apply methods take event records

Change `BudgetTrackerState.Apply` to take `ApiCallRecorded` directly.
Change `PollSchedulerState.Apply` to take the domain `DataChanged` message
directly. Decomposition happens inside the method.

### Journal cleanup after snapshot

Add `DeleteMessages` + `DeleteSnapshots` in `SaveSnapshotSuccess` handlers,
matching FunkArr's pattern. Handle `DeleteMessagesSuccess/Failure` and
`DeleteSnapshotsSuccess/Failure`. This prevents unbounded journal growth.

## Non-goals

- Replacing the health-check singleton with actor-based Ask queries. The
  current singleton + `IHealthCheck` approach is idiomatic ASP.NET Core and
  works; the problem is the mutable API, not the architectural pattern.
- Changing persistence wire-format names (`[JsonProperty]` values). Only type
  names change — wire compatibility is preserved.
- Auditing actor, test, or C# conventions. The audit found them clean.
- Changing the init-property style with `[JsonProperty]` to positional records.
  The `[JsonProperty]` style gives stable wire names — a project-specific
  refinement of FunkArr that is more robust for extend-only contracts.
- Extracting a ProcessCommand pattern. FunkArr's actual code constructs events
  inline in actor handlers — the ProcessCommand tuple return in the skill doc
  is an ideal, not the practiced pattern.

## API-budget impact

None — no change to polling, request patterns, or API calls.

## Risks

- **Snapshot rename in persistence** — the record type name changes but
  `[JsonProperty]` wire names are preserved. Existing persisted data
  deserializes correctly. Zero runtime risk.
- **Health monitor API change** — all consumers are internal to the host
  project. No external API surface.
