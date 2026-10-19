## MODIFIED Requirements

### Requirement: One stateful stage computes all analysis per forecast
For every forecast the `StatefulSelectMany` stage SHALL update a `ModelForecasts` held in the stage closure, build a `LocationConsensus`, and emit analysis events. The sensor readings side-channel is removed: the `ZipWith(sensors)` stage is eliminated, and `IndexAnalyzer.Compute` no longer receives a `SensorReadings` parameter. Indoor temperature for VPD and FrostProtection uses the configured `IndoorTemp` default from IndexOptions.

#### Scenario: Events per forecast without sensors
- **WHEN** a forecast arrives and all analysis features are enabled
- **THEN** the stage emits `ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed` without requiring sensor data

#### Scenario: Index computation uses config indoor temp
- **WHEN** the IndexAnalyzer computes VPD or FrostProtection
- **THEN** it uses the configured `IndoorTemp` value from IndexOptions, not live sensor readings
