## Purpose

Defines test coverage gaps to close: IndexAnalyzer sensor-override tests with real sensor data, and the requirement that tests pass reliably without flakiness.
## Requirements
### Requirement: IndexAnalyzer sensor-override is tested with real sensor data

`IndexAnalyzer.OverrideIndoorTemp` SHALL be tested with non-null sensor
readings to verify the sensor fallback chain (live reading → config →
hardcoded default).

#### Scenario: Sensor override applies indoor temperature
- **WHEN** IndexAnalyzer runs with sensor readings containing an indoor temperature reading
- **THEN** the indoor temperature in the computation SHALL use the sensor value instead of the config default

#### Scenario: Sensor override filters by location
- **WHEN** IndexAnalyzer runs with sensor data for a different location
- **THEN** the sensor override SHALL NOT apply to the current location

### Requirement: No flaky tests

All tests SHALL pass reliably on repeated runs. A test that intermittently
fails SHALL be identified and stabilized.

#### Scenario: Core.Tests reliability
- **WHEN** `Njord.Core.Tests` is run 3 times consecutively
- **THEN** all runs SHALL produce identical pass/fail results with zero failures

### Requirement: Behavioural production units have a spec
Every production class with behaviour (not marker interfaces, records without logic or generated code) SHALL be exercised by a spec that names it or by a mapping or contract spec that pins its output. The gateway budget gate, the sensor gRPC service, both health checks, the application setup and the persistence provider selection SHALL have direct specs.

#### Scenario: Token bucket refill
- **WHEN** the gate has consumed its per-minute allowance and `FakeTimeProvider` advances one minute
- **THEN** the next acquisition succeeds, and before the advance it is rejected with the budget reason

#### Scenario: Sensor service rejects an unknown sensor
- **WHEN** a sensor reading with an unknown kind is posted to `SensorGrpcService`
- **THEN** the call returns the documented rejection and the SensorHub receives no update

#### Scenario: Health check reports stale pipeline
- **WHEN** the last successful poll is older than the health threshold
- **THEN** `PipelineHealthCheck` reports unhealthy with the age in its description

#### Scenario: SQL persistence selection
- **WHEN** the provider is `PostgreSql` with a connection string, and when it is `Sqlite` without one
- **THEN** the selected provider name and connection string match the persistence-provider rules without opening a database

### Requirement: Tests use no wall-clock waiting
Production specs SHALL NOT call `Task.Delay` or `Thread.Sleep` to wait for behaviour and SHALL NOT use `DateTimeOffset.UtcNow`/`DateTime.UtcNow` for test data or deadlines. Waits SHALL be bounded `AwaitAssert`/`AwaitCondition`/`ExpectMsg` calls or `FakeTimeProvider` advances.

#### Scenario: Read model wait
- **WHEN** `ForecastReadModelSpec` waits for a stream event
- **THEN** it uses a bounded wait helper with a descriptive timeout failure and no `Task.Delay`

#### Scenario: Enrichment test data
- **WHEN** `EnrichmentKindSpec` needs timestamps
- **THEN** it uses a fixed timestamp, not the wall clock

### Requirement: Documentation names only things that exist
`AGENTS.md`, `CLAUDE.md` and the project skills SHALL NOT reference spec files, helper methods or projects that do not exist, and the documented test counts SHALL equal the counts the test projects report.

#### Scenario: Stale helper reference
- **WHEN** `AGENTS.md` mentions a test helper
- **THEN** a symbol with that name exists in `Njord.Tests.Shared`

