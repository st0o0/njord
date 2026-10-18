## ADDED Requirements

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
