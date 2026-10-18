# sensor-hub Specification

## Purpose

Domain model and actor for receiving, validating, aggregating, and expiring external sensor readings. Defines SensorKind metadata, plausibility ranges, aggregation strategies, staleness expiry, the SensorReadings query model, the readings-map push to the pipeline, and SensorOptions configuration.
## Requirements
### Requirement: SensorKind domain enum
The system SHALL define a closed `SensorKind` enum representing physical quantities that enrichments can consume from external sensors. Each kind SHALL have associated metadata: unit of measurement, plausibility range (min/max), and aggregation strategy (Average, Sum, or Latest).

#### Scenario: Initial SensorKind values
- **WHEN** the system starts
- **THEN** exactly the following SensorKind values are available: `IndoorTemperature` (°C, -10..60, Average) and `IndoorHumidity` (%, 0..100, Average)

#### Scenario: Unknown SensorKind rejected
- **WHEN** a reading with an unrecognized or unspecified SensorKind is received
- **THEN** the system SHALL reject it with an appropriate error

### Requirement: Plausibility validation per SensorKind
The SensorHub SHALL reject readings whose value falls outside the plausibility range defined by the SensorKind metadata. The rejection SHALL include the reason and the valid range.

#### Scenario: Reading within plausible range accepted
- **WHEN** a reading `(IndoorTemperature, Value=23.5)` is received
- **THEN** the reading SHALL be accepted (23.5 is within -10..60)

#### Scenario: Reading outside plausible range rejected
- **WHEN** a reading `(IndoorTemperature, Value=85.0)` is received
- **THEN** the reading SHALL be rejected with reason indicating the valid range is -10..60

### Requirement: Staleness expiry
The SensorHub SHALL expire readings whose `MeasuredAt` timestamp is older than the configured staleness TTL. Expired readings SHALL be excluded from aggregation and readings responses. The SensorHub SHALL run a periodic cleanup timer.

#### Scenario: Fresh reading included in readings
- **WHEN** a reading was stored 30 seconds ago
- **AND** the staleness TTL is 7200 seconds
- **THEN** the reading SHALL be included in the `SensorReadings`

#### Scenario: Stale reading excluded from readings
- **WHEN** a reading was stored 8000 seconds ago
- **AND** the staleness TTL is 7200 seconds
- **THEN** the reading SHALL NOT be included in the `SensorReadings`

#### Scenario: Partial staleness with multiple sources
- **WHEN** source "wohnzimmer" has a fresh reading (23.5) and source "schlafzimmer" has an expired reading (21.0)
- **AND** the kind uses Average aggregation
- **THEN** the `SensorReadings` SHALL contain value `23.5` with source count `1`

### Requirement: SensorReadings domain record
The system SHALL define a `SensorReadings` record containing the aggregated sensor state for a single location. It SHALL provide a `Get(SensorKind)` method returning the aggregated value as `double?` (null if no readings exist for that kind).

#### Scenario: Get existing kind
- **WHEN** a `SensorReadings` contains `IndoorTemperature = 22.5`
- **AND** `Get(SensorKind.IndoorTemperature)` is called
- **THEN** the result SHALL be `22.5`

#### Scenario: Get missing kind
- **WHEN** a `SensorReadings` does not contain `IndoorHumidity`
- **AND** `Get(SensorKind.IndoorHumidity)` is called
- **THEN** the result SHALL be `null`

### Requirement: Sensor reading timestamp fallback
`SensorGrpcService` SHALL use the injected `TimeProvider` to generate fallback timestamps when a gRPC request does not include a `MeasuredAt` value. It SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: Missing MeasuredAt uses TimeProvider
- **WHEN** a `SensorService.Push` or `StreamPush` gRPC request arrives without a `MeasuredAt` field
- **THEN** the service SHALL use `TimeProvider.GetUtcNow()` as the fallback timestamp

#### Scenario: Provided MeasuredAt is preserved
- **WHEN** a `SensorService.Push` or `StreamPush` gRPC request includes a `MeasuredAt` field
- **THEN** the service SHALL use the provided timestamp, ignoring `TimeProvider`

### Requirement: SensorOptions configuration
The system SHALL define a `SensorOptions` configuration class with `Enabled` (default: true) and `StalenessSeconds` (default: 7200). Validation SHALL ensure `StalenessSeconds` is positive.

#### Scenario: Default configuration
- **WHEN** no sensor configuration is provided
- **THEN** `Enabled` SHALL be `true` and `StalenessSeconds` SHALL be `7200`

#### Scenario: Invalid staleness rejected
- **WHEN** `StalenessSeconds` is set to `0` or negative
- **THEN** startup validation SHALL fail with an appropriate error message

### Requirement: SensorHub actor stores and aggregates the latest readings
The SensorHub actor SHALL store the most recent `SensorReading` per unique key of (Location, SensorKind, Source). It SHALL accept `UpdateReading` messages to store new readings and `QuerySensorReadings` messages to return the current aggregated state for a location as `SensorReadingsResult`, or `SensorReadingsNotAvailable` when the location has no readings.

#### Scenario: Store and retrieve a single reading
- **WHEN** a reading `(Location="Luzern", Kind=IndoorTemperature, Source="wohnzimmer", Value=23.5)` is stored
- **AND** a `QuerySensorReadings("Luzern")` is requested
- **THEN** the reply SHALL be `SensorReadingsResult` containing `IndoorTemperature` with value `23.5` and source count `1`

#### Scenario: Multiple sources aggregated by Average
- **WHEN** readings `(Luzern, IndoorTemperature, "wohnzimmer", 23.5)` and `(Luzern, IndoorTemperature, "schlafzimmer", 21.0)` are stored
- **AND** a `QuerySensorReadings("Luzern")` is requested
- **THEN** the reply SHALL be `SensorReadingsResult` containing `IndoorTemperature` with value `22.25` and source count `2`

#### Scenario: QuerySensorReadings for location with no readings
- **WHEN** a `QuerySensorReadings("Unknown")` is requested
- **AND** no readings exist for that location
- **THEN** the reply SHALL be `SensorReadingsNotAvailable`

### Requirement: SensorHub pushes readings maps to the pipeline
The SensorHub SHALL push `LocationSensorReadings` — an immutable map from `LocationId` to the aggregated `SensorReadings` of every location that has at least one fresh reading, with the staleness window applied at push time — to the pipeline resolved through the `IForecastPipeline` marker: after every accepted `UpdateReading`, on every expiry tick (every 60 seconds, also when nothing expired), and once when the pipeline is first resolved after the SensorHub started. While no pipeline is registered the SensorHub SHALL skip the push without failing; a rejected reading SHALL NOT trigger a push.

#### Scenario: Accepted reading is pushed
- **WHEN** the pipeline is registered and a reading `(Luzern, IndoorTemperature, "wohnzimmer", 23.5)` is accepted
- **THEN** the pipeline receives `LocationSensorReadings` whose entry for "Luzern" holds `IndoorTemperature = 23.5`

#### Scenario: Map covers every location with fresh readings
- **WHEN** fresh readings exist for "Luzern" and "Bern" and another reading is accepted
- **THEN** the pushed map has entries for both "Luzern" and "Bern"

#### Scenario: Stale reading is left out of the map
- **WHEN** a reading older than the staleness window is stored for "Luzern" and no fresh reading exists for it
- **THEN** the pushed map has no entry for "Luzern"

#### Scenario: Rejected reading pushes nothing
- **WHEN** a reading outside its plausibility range is rejected
- **THEN** no `LocationSensorReadings` is pushed for it

#### Scenario: No pipeline registered
- **WHEN** a reading is accepted while no pipeline is registered
- **THEN** the SensorHub replies `PushResult(true, null)` and keeps running

