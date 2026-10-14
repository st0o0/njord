## REMOVED Requirements

### Requirement: Apply methods take event records
**Reason**: The scenario named the removed `PollSchedulerState`; the rule itself is unchanged and restated for the poll worker.
**Migration**: See "Apply methods take persistence event records".

## ADDED Requirements

### Requirement: Apply methods take persistence event records
State `Apply` methods SHALL take the persistence event record directly, not
decomposed primitive values. Decomposition happens inside the method. This
matches FunkArr where `Recover<Event>(evt => _state = _state.Apply(evt))`.

#### Scenario: BudgetTrackerState.Apply takes ApiCallRecorded
- **GIVEN** an `ApiCallRecorded` event
- **WHEN** applied to `BudgetTrackerState`
- **THEN** the `Apply` method signature accepts `ApiCallRecorded` (not `int weight, DateTimeOffset now`)

#### Scenario: PollWorkerState.Apply takes the worker's events
- **GIVEN** a `ForecastChanged`, `ForecastUnchanged`, `PollFailed`, `PollDeferred`, `PollingStarted` or `PollingStopped` event
- **WHEN** applied to `PollWorkerState`
- **THEN** the `Apply` overload accepts that event record, and the worker's command handler and its recovery call the same overload
