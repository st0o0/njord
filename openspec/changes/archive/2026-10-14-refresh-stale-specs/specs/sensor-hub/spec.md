## MODIFIED Requirements

### Requirement: SensorHub actor stores and aggregates the latest readings
The SensorHub actor SHALL store the most recent `SensorReading` per unique key of (Location, SensorKind, Source). It SHALL accept `UpdateReading` messages to store new readings and `QuerySensorSnapshot` messages to return the current aggregated state for a location as `SensorSnapshotResult`, or `SensorSnapshotNotAvailable` when the location has no readings.

#### Scenario: Store and retrieve a single reading
- **WHEN** a reading `(Location="Luzern", Kind=IndoorTemperature, Source="wohnzimmer", Value=23.5)` is stored
- **AND** a `QuerySensorSnapshot("Luzern")` is requested
- **THEN** the reply SHALL be `SensorSnapshotResult` containing `IndoorTemperature` with value `23.5` and source count `1`

#### Scenario: Multiple sources aggregated by Average
- **WHEN** readings `(Luzern, IndoorTemperature, "wohnzimmer", 23.5)` and `(Luzern, IndoorTemperature, "schlafzimmer", 21.0)` are stored
- **AND** a `QuerySensorSnapshot("Luzern")` is requested
- **THEN** the reply SHALL be `SensorSnapshotResult` containing `IndoorTemperature` with value `22.25` and source count `2`

#### Scenario: QuerySensorSnapshot for location with no readings
- **WHEN** a `QuerySensorSnapshot("Unknown")` is requested
- **AND** no readings exist for that location
- **THEN** the reply SHALL be `SensorSnapshotNotAvailable`

## REMOVED Requirements

### Requirement: HistoryAnalyzer TimeProvider is non-optional
**Reason**: `HistoryAnalyzer` and the history feature no longer exist.
**Migration**: None.
