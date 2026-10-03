## MODIFIED Requirements

### Requirement: Index ventilation uses live IndoorTemperature with fallback
The index enrichment SHALL use the live `IndoorTemperature` from the `SensorSnapshot` when available. If no live reading exists, it SHALL fall back to the configured `IndexPreferences.IndoorTemp`. If neither is set, it SHALL use the hardcoded default of 22.0.

#### Scenario: Live sensor value used
- **WHEN** the SensorSnapshot contains `IndoorTemperature = 24.5`
- **AND** the config `IndoorTemp` is `22.0`
- **THEN** the Ventilation score SHALL be computed with indoor temperature `24.5`

#### Scenario: No sensor value falls back to config
- **WHEN** the SensorSnapshot is null or does not contain `IndoorTemperature`
- **AND** the config `IndoorTemp` is `20.0`
- **THEN** the Ventilation score SHALL be computed with indoor temperature `20.0`

#### Scenario: No sensor and no config falls back to default
- **WHEN** the SensorSnapshot is null
- **AND** no config `IndoorTemp` is set
- **THEN** the Ventilation score SHALL be computed with indoor temperature `22.0`
