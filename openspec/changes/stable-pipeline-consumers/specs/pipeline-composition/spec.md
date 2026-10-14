## MODIFIED Requirements

### Requirement: Settings side channel
`ForecastPipeline` SHALL feed analysis settings into the graph through a `Source.ActorRef` side channel (buffer 10, `DropHead`), started with the startup settings and repeated with `RepeatPrevious`, so every forecast is analyzed with the latest `AnalysisSettings`. When it starts, after materializing its graph, the pipeline SHALL send `QueryAnalysisSettings` to the config manager (`IConfigManager`) if one is registered, and SHALL forward the settings of the `AnalysisSettingsResult` reply into the side channel, so a restarted pipeline analyzes with the current settings instead of the startup settings. It SHALL NOT wait for that reply: until it arrives, or when no config manager is registered (logged at Debug), the startup settings apply. On `AnalysisSettingsChanged` the actor SHALL forward the settings to the side channel, so a change applies from the next forecast on and never in the middle of one computation.

#### Scenario: Settings change applies to the next forecast
- **WHEN** `ConfigManager` sends `AnalysisSettingsChanged` with new alert thresholds
- **THEN** the next forecast is analyzed with the new thresholds and a computation already running is not affected

#### Scenario: A started pipeline uses the current settings
- **WHEN** the config manager holds settings that differ from the startup settings and the pipeline starts (or restarts)
- **THEN** the pipeline analyzes forecasts with the config manager's current settings from the next forecast on, as after an `AnalysisSettingsChanged`, although no change was sent

#### Scenario: No config manager registered
- **WHEN** the pipeline starts and no config manager is registered
- **THEN** the pipeline starts normally and analyzes forecasts with the startup settings

### Requirement: Live sensor snapshot per forecast
`ForecastPipeline` SHALL accept `SensorSnapshots` messages (an immutable map from `LocationId` to `SensorSnapshot`, pushed by the SensorHub) and feed each into a sensor side channel (`Source.ActorRef`, buffer 10, `DropHead`, started with an empty map and repeated with `RepeatPrevious`) that is zipped onto every forecast, so every forecast is analyzed with the latest pushed map and a new map never applies in the middle of one computation. When it starts, after materializing its graph, the pipeline SHALL send `RequestSensorSnapshots` to the SensorHub if one is registered; the SensorHub SHALL answer the sender with its current `SensorSnapshots` (staleness window applied), so a restarted pipeline has the current map before the next accepted reading or expiry tick. For each forecast the stage SHALL pass the map's entry for the forecast's location (matched case-insensitively) to `IndexAnalyzer`, or `null` when the map has no entry for it. The pipeline SHALL NOT ask the SensorHub per forecast and SHALL NOT wait for the start reply, and a missing or late push SHALL NOT drop, delay or fail the analysis of a forecast.

#### Scenario: Live indoor temperature reaches the indices
- **WHEN** the latest pushed map holds `IndoorTemperature = 24.5` for location "Luzern" and a forecast for "Luzern" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives a snapshot with `IndoorTemperature = 24.5` and the index result reflects it instead of the configured indoor temperature

#### Scenario: Readings are matched to the forecast's location
- **WHEN** the latest pushed map holds readings for "Bern" only and a forecast for "Lucerne" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives `null` for "Lucerne"

#### Scenario: No sensor data yet
- **WHEN** no map has been pushed yet, or the map has no entry for the forecast's location
- **THEN** `IndexAnalyzer.Compute` receives `null` for the sensors and uses configured or default indoor values

#### Scenario: SensorHub unavailable
- **WHEN** no SensorHub is registered and nothing is pushed
- **THEN** the forecast is analyzed with `null` sensors and all its pipeline events are emitted

#### Scenario: Stale readings are not used
- **WHEN** the only reading for the location is older than the configured staleness window when the SensorHub pushes its map
- **THEN** the pushed map has no entry for that location and `IndexAnalyzer.Compute` receives `null` for it

#### Scenario: Location keys match case-insensitively
- **WHEN** the latest pushed map holds a snapshot keyed "LUCERNE" and a forecast for "lucerne" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives that snapshot

#### Scenario: A started pipeline asks for the current sensor map
- **WHEN** the SensorHub holds a fresh `IndoorTemperature` reading for "Luzern" and the pipeline starts (or restarts)
- **THEN** the pipeline's forecasts for "Luzern" receive that reading from the next forecast on, as after a push, without a new reading or an expiry tick in between

#### Scenario: SensorHub answers a snapshot request
- **WHEN** the SensorHub receives `RequestSensorSnapshots`
- **THEN** it replies to the sender with a `SensorSnapshots` map of every location with fresh readings
