## MODIFIED Requirements

### Requirement: All configurable options documented
The `docker-compose.example.yml` SHALL contain every configurable environment variable of the bound options types — `NjordOptions` (with `ParameterOptions`), `PollingOptions` (including `BudgetOverride`), `OpenMeteoOptions`, `StorageOptions`, `PersistenceOptions`, `MqttOptions`, `SensorOptions`, `ConsensusOptions`, `AlertOptions`, `DerivedOptions`, `TrendOptions` and `IndexOptions` — as environment variable entries, using the property names those types declare. Required values SHALL be uncommented; optional values SHALL be commented with their default values.

#### Scenario: Required MQTT host is uncommented
- **WHEN** the example compose file is read
- **THEN** `Njord__Mqtt__Host` is present as an uncommented variable with a placeholder value

#### Scenario: Optional values show defaults
- **WHEN** the example compose file is read
- **THEN** optional variables like `Njord__Mqtt__Port`, `Njord__PollInterval`, `Njord__Enrichment__Consensus__Enabled` are present as commented entries showing their default values

#### Scenario: All enrichment toggles are listed
- **WHEN** the example compose file is read
- **THEN** every analysis feature (Consensus, Alerts, Derived, Trends, Indices) has its `Enabled` toggle listed, and no removed feature (History) appears
