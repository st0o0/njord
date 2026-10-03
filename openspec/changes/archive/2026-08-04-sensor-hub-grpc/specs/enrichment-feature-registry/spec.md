## MODIFIED Requirements

### Requirement: Stateless enrichment interface
The `IStatelessEnrichment` interface SHALL define a `Compute` method that accepts a `ConsensusSnapshot` and an optional `SensorSnapshot?` parameter. Implementations that do not use sensor data SHALL ignore the parameter.

#### Scenario: Compute called with sensor data
- **WHEN** the enrichment pipeline runs with available sensor readings
- **THEN** `Compute` SHALL be called with a non-null `SensorSnapshot`

#### Scenario: Compute called without sensor data
- **WHEN** the enrichment pipeline runs without any sensor readings
- **THEN** `Compute` SHALL be called with a null `SensorSnapshot`

### Requirement: Stateful enrichment interface
The `IStatefulEnrichment` interface SHALL define a `Compute` method that accepts a `ConsensusSnapshot`, an optional previous `ConsensusSnapshot?`, and an optional `SensorSnapshot?` parameter. Implementations that do not use sensor data SHALL ignore the parameter.

#### Scenario: Compute called with sensor data
- **WHEN** the enrichment pipeline runs with available sensor readings
- **THEN** `Compute` SHALL be called with a non-null `SensorSnapshot`

#### Scenario: Compute called without sensor data
- **WHEN** the enrichment pipeline runs without any sensor readings
- **THEN** `Compute` SHALL be called with a null `SensorSnapshot`
