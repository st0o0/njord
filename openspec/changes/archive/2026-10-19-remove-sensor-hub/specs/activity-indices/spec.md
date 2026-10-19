## MODIFIED Requirements

### Requirement: IndexAnalyzer signature without SensorReadings
`IndexAnalyzer.Compute` SHALL accept `LocationConsensus` and `IndexSettings` (and optionally a configured indoor temperature from IndexOptions). The `SensorReadings?` parameter is removed. VPD and FrostProtection calculations SHALL use the configured `IndoorTemp` default value from IndexOptions. The sensor fallback chain (live reading → config → hardcoded) is simplified to: config value → hardcoded default.

#### Scenario: VPD uses configured indoor temperature
- **WHEN** IndexAnalyzer computes VPD
- **THEN** it uses `IndexOptions.IndoorTemp` (default 20°C) for the indoor temperature component

#### Scenario: FrostProtection uses configured indoor temperature
- **WHEN** IndexAnalyzer computes FrostProtection
- **THEN** it uses `IndexOptions.IndoorTemp` (default 20°C) for the indoor temperature baseline

#### Scenario: No sensor parameter in signature
- **WHEN** IndexAnalyzer.Compute is called by the pipeline
- **THEN** it does not accept a SensorReadings parameter
