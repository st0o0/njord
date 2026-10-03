## REMOVED Requirements

### Requirement: SensorHub actor stores latest readings
**Reason**: Scenarios are dropped or renamed so the text matches the code (OpenSpec refuses to drop scenarios through MODIFIED): Multiple sources aggregated by Sum; Latest aggregation uses most recent value; GetSnapshot for location with no readings.
**Migration**: Re-added in this delta as `SensorHub actor stores and aggregates the latest readings` with the corrected scenarios.

## MODIFIED Requirements

### Requirement: SensorKind domain enum
The system SHALL define a closed `SensorKind` enum representing physical quantities that enrichments can consume from external sensors. Each kind SHALL have associated metadata: unit of measurement, plausibility range (min/max), and aggregation strategy (Average, Sum, or Latest).

#### Scenario: Initial SensorKind values
- **WHEN** the system starts
- **THEN** exactly the following SensorKind values are available: `IndoorTemperature` (°C, -10..60, Average) and `IndoorHumidity` (%, 0..100, Average)

#### Scenario: Unknown SensorKind rejected
- **WHEN** a reading with an unrecognized or unspecified SensorKind is received
- **THEN** the system SHALL reject it with an appropriate error

### Requirement: SensorSnapshot domain record
The system SHALL define a `SensorSnapshot` record containing the aggregated sensor state for a single location. It SHALL provide a `Get(SensorKind)` method returning the aggregated value as `double?` (null if no readings exist for that kind).

#### Scenario: Get existing kind
- **WHEN** a snapshot contains `IndoorTemperature = 22.5`
- **AND** `Get(SensorKind.IndoorTemperature)` is called
- **THEN** the result SHALL be `22.5`

#### Scenario: Get missing kind
- **WHEN** a snapshot does not contain `IndoorHumidity`
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

## ADDED Requirements

### Requirement: SensorHub actor stores and aggregates the latest readings
The SensorHub actor SHALL store the most recent `SensorReading` per unique key of (Location, SensorKind, Source). It SHALL accept `UpdateReading` messages to store new readings and `QuerySensorSnapshot` messages to return the current aggregated state for a location as `SensorSnapshotFound`, or `SensorSnapshotNotFound` when the location has no readings.

#### Scenario: Store and retrieve a single reading
- **WHEN** a reading `(Location="Luzern", Kind=IndoorTemperature, Source="wohnzimmer", Value=23.5)` is stored
- **AND** a `QuerySensorSnapshot("Luzern")` is requested
- **THEN** the reply SHALL be `SensorSnapshotFound` containing `IndoorTemperature` with value `23.5` and source count `1`

#### Scenario: Multiple sources aggregated by Average
- **WHEN** readings `(Luzern, IndoorTemperature, "wohnzimmer", 23.5)` and `(Luzern, IndoorTemperature, "schlafzimmer", 21.0)` are stored
- **AND** a `QuerySensorSnapshot("Luzern")` is requested
- **THEN** the reply SHALL be `SensorSnapshotFound` containing `IndoorTemperature` with value `22.25` and source count `2`

#### Scenario: QuerySensorSnapshot for location with no readings
- **WHEN** a `QuerySensorSnapshot("Unknown")` is requested
- **AND** no readings exist for that location
- **THEN** the reply SHALL be `SensorSnapshotNotFound`
