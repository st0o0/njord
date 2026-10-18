## Purpose

Defines the immutable state record pattern for SensorHub: domain state (readings, expiry, aggregation) extracted into a pure SensorHubState record with extension methods, following the FunkArr actor-state pattern.
## Requirements

### Requirement: SensorHub uses immutable state record

SensorHub SHALL hold its domain state (sensor readings, expiry logic, aggregation) in a `SensorHubState` immutable record with `SensorHubStateExtensions`, following the FunkArr actor-state pattern. The actor SHALL be a thin shell that delegates all domain logic to state extension methods.

#### Scenario: State record holds readings
- **WHEN** SensorHub receives an `UpdateReading` command
- **THEN** the actor SHALL call an `Apply` extension method on its state record and replace `_state` with the returned new record

#### Scenario: Actor is a thin shell
- **WHEN** inspecting SensorHub's message handlers
- **THEN** each handler SHALL delegate domain logic (validation, aggregation, expiry) to `SensorHubState` extension methods and only perform actor-specific operations (Tell, Timers)

### Requirement: SensorHubState has Empty field

`SensorHubState` SHALL expose an `Empty` static readonly field returning the initial empty state (no readings).

#### Scenario: Empty state has no readings
- **WHEN** `SensorHubState.Empty` is accessed
- **THEN** it SHALL return a state with an empty readings collection

### Requirement: SensorHubState exposes Apply for readings

`SensorHubStateExtensions` SHALL provide an `Apply` method that accepts a `SensorReading` and returns a new state with the reading stored, keyed by (Location, SensorKind, Source).

#### Scenario: Apply stores a valid reading
- **WHEN** `Apply` is called with a valid `SensorReading`
- **THEN** the returned state SHALL contain that reading under its composite key

#### Scenario: Apply overwrites a reading for the same key
- **WHEN** `Apply` is called with a reading whose (Location, Kind, Source) matches an existing entry
- **THEN** the returned state SHALL contain only the newer reading for that key

### Requirement: SensorHubState exposes validation as a pure function

`SensorHubStateExtensions` SHALL provide a `Validate` method that checks a reading against `SensorKindMetadata` plausibility ranges and returns a validation result. The actor SHALL use this method instead of inline validation logic.

#### Scenario: Valid reading passes validation
- **WHEN** `Validate` is called with a reading whose value is within the plausible range for its `SensorKind`
- **THEN** it SHALL return a success result

#### Scenario: Out-of-range reading fails validation
- **WHEN** `Validate` is called with a reading whose value exceeds the plausible range
- **THEN** it SHALL return a failure result with a message describing the range violation

### Requirement: SensorHubState exposes readings query

`SensorHubStateExtensions` SHALL provide a `GetReadings` method that accepts a location and the current time, filters out expired readings, aggregates by `SensorKind`, and returns a `SensorReadings` or indicates no data is available.

#### Scenario: GetReadings aggregates non-expired readings
- **WHEN** `GetReadings` is called with a location that has fresh readings
- **THEN** it SHALL return a `SensorReadings` with aggregated values per `SensorKind`

#### Scenario: GetReadings excludes expired readings
- **WHEN** `GetReadings` is called and some readings are older than the staleness threshold
- **THEN** those readings SHALL be excluded from the aggregation

### Requirement: SensorHubState exposes expiry as a pure function

`SensorHubStateExtensions` SHALL provide an `Expire` method that accepts the current time and staleness threshold and returns a new state with expired readings removed.

#### Scenario: Expire removes stale readings
- **WHEN** `Expire` is called and readings older than the staleness threshold exist
- **THEN** the returned state SHALL not contain those readings

#### Scenario: Expire preserves fresh readings
- **WHEN** `Expire` is called and all readings are within the staleness threshold
- **THEN** the returned state SHALL contain all readings unchanged

### Requirement: State-only unit tests for SensorHubState

`SensorHubState` SHALL have dedicated unit tests that verify `Apply`, `Validate`, `GetReadings`, and `Expire` as pure functions without any actor infrastructure.

#### Scenario: Pure function test coverage
- **WHEN** the test suite runs
- **THEN** SensorHubState SHALL have tests covering: empty state, applying readings, validation pass/fail, readings aggregation with mixed fresh/expired readings, expiry, and multiple sources for the same sensor kind
