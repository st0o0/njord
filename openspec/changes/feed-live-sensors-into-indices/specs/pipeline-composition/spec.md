## ADDED Requirements

### Requirement: Settings side channel
`ForecastPipeline` SHALL feed analysis settings into the graph through a `Source.ActorRef` side channel (buffer 10, `DropHead`), started with the startup settings and repeated with `RepeatPrevious`, so every forecast is analyzed with the latest `AnalysisSettings`. On `AnalysisSettingsChanged` the actor SHALL store the settings and forward them to the side channel, so a change applies from the next forecast on and never in the middle of one computation.

#### Scenario: Settings change applies to the next forecast
- **WHEN** `ConfigManager` sends `AnalysisSettingsChanged` with new alert thresholds
- **THEN** the next forecast is analyzed with the new thresholds and a computation already running is not affected

### Requirement: Live sensor snapshot per forecast
For every forecast, `ForecastPipeline` SHALL ask the SensorHub (`ISensorHub`) with `QuerySensorSnapshot` for the forecast's location before analyzing it, one forecast at a time and in arrival order, and SHALL pass the snapshot of a `SensorSnapshotResult` to `IndexAnalyzer`. It SHALL pass `null` when the reply is `SensorSnapshotNotAvailable`, when the SensorHub is not registered, or when no reply arrives within a bounded timeout. A failed or missing sensor lookup SHALL NOT drop, delay beyond that timeout or fail the analysis of the forecast. The pipeline SHALL NOT accept pushed `SensorSnapshot` messages.

#### Scenario: Live indoor temperature reaches the indices
- **WHEN** the SensorHub holds a fresh `IndoorTemperature` reading of 24.5 for location "Luzern" and a forecast for "Luzern" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives a snapshot with `IndoorTemperature = 24.5` and the index result reflects it instead of the configured indoor temperature

#### Scenario: Readings are matched to the forecast's location
- **WHEN** the SensorHub holds readings for "Luzern" only and a forecast for "Borken" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives `null` for "Borken"

#### Scenario: No sensor data yet
- **WHEN** the SensorHub has no fresh reading for the forecast's location
- **THEN** `IndexAnalyzer.Compute` receives `null` for the sensors and uses configured or default indoor values

#### Scenario: SensorHub unavailable
- **WHEN** the SensorHub is not registered or does not reply within the timeout
- **THEN** the forecast is analyzed with `null` sensors and all its pipeline events are emitted

#### Scenario: Stale readings are not used
- **WHEN** the only reading for the location is older than the configured staleness window
- **THEN** `IndexAnalyzer.Compute` receives `null` for that location

## REMOVED Requirements

### Requirement: Sensor and settings side channels
**Reason**: The sensor side channel held one snapshot for all locations and was never fed. Sensor data is now looked up per forecast and location (see "Live sensor snapshot per forecast"); the settings channel is unchanged and specified in "Settings side channel".
**Migration**: Nothing sends `SensorSnapshot` to `ForecastPipeline`. Push readings through the sensor gRPC service; the pipeline reads them from the SensorHub.
