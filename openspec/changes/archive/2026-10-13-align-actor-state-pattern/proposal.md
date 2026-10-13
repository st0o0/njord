## Why

The FunkArr actor-state pattern is defined in the `actor-state-pattern` spec and partially implemented. BudgetTracker and PollScheduler have state records but deviate from the canonical pattern in small ways (static method vs field for `Empty`, `FromPersistence` placement, no `ProcessCommand`). SensorHub has domain state inline in the actor with no state record at all. The three stream-oriented actors (ForecastPipeline, OpenMeteoGateway, MqttConnection) are readable but inconsistent in how they organize fields, private messages, and lifecycle phases. Aligning everything now — while the actor count is small — establishes the convention before more actors are added.

## What Changes

- **BudgetTracker**: Make `Empty` a `static readonly` field (move TimeProvider dependency out), move `FromPersistence` from extension method to static method on the record, introduce `ProcessCommand` tuple pattern for command → event flow.
- **PollScheduler**: Add `GetPersistenceState()` and real `FromPersistence()` for snapshot recovery (currently a no-op), move poll-cycle tracking and scheduling helpers that are pure state queries into state extensions where they belong.
- **SensorHub**: Extract `_readings` dictionary, expiry logic, and aggregation into `SensorHubState` record + `SensorHubStateExtensions`. Actor becomes a thin shell.
- **Stream actors** (ForecastPipeline, OpenMeteoGateway, MqttConnection): Consistent field ordering (deps → runtime refs → log), extract shared gateway-resolve retry pattern into a documented convention, clean up Become-phase organization. No forced State record — these actors have infrastructure state, not domain state.

## Non-goals

- Changing actor behavior, message protocols, or persistence wire format.
- Adding new tests for stream actors (they already have coverage). New state-only tests are added only for SensorHub's extracted state.
- Extracting a shared base class or utility for the gateway-resolve retry pattern — document the convention, don't abstract it.
- Changing persistence record names or `[JsonProperty]` strings.

## Capabilities

### New Capabilities
- `sensor-hub-state`: Extract SensorHub domain state into an immutable state record following the FunkArr pattern.
- `stream-actor-conventions`: Establish field ordering and lifecycle-phase conventions for non-persistent stream-oriented actors.

### Modified Capabilities
- `actor-state-pattern`: Tighten requirements to match FunkArr canonical form — `Empty` as static readonly field, `FromPersistence` as static method on record, `ProcessCommand` tuple pattern.
- `budget-tracker-persistence`: BudgetTrackerState.Empty changes from method to field; FromPersistence moves from extension to static method on record.

## Impact

- `src/Njord.Budget/BudgetTracker.cs` + `BudgetTrackerState.cs` — state record restructuring
- `src/Njord.Pipeline/PollScheduler.cs` + `PollSchedulerState.cs` — add snapshot persistence, move logic to state
- `src/Njord.SensorHub/SensorHub.cs` — thin-shell refactor, new `SensorHubState.cs`
- `src/Njord.SensorHub.Tests/` — new state-only unit tests
- `src/Njord.Pipeline/ForecastPipeline.cs` — field reordering, Become cleanup
- `src/Njord.OpenMeteo/OpenMeteoGateway.cs` — field reordering
- `src/Njord.Mqtt/MqttConnection.cs` — field reordering, Become cleanup
- `src/Njord.Budget.Tests/` — adapt to new Empty/FromPersistence API
- `src/Njord.Pipeline.Tests/` — adapt to new PollScheduler snapshot support
- No wire-format or API changes. No new dependencies.
