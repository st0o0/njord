## MODIFIED Requirements

### Requirement: Entity expectations are derived from config

The test plan SHALL derive expected entity IDs from the Docker Compose configuration (locations × models × enrichment features) rather than hardcoding them.

#### Scenario: Config-driven entity count
- **WHEN** the Docker Compose config has 1 location, 2 models, and all 5 analysis features enabled
- **THEN** the test plan expects approximately 47 entities: 3 weather, 38 sensors, 4 binary_sensors, 1 event, 1 button

#### Scenario: Entity ID pattern
- **WHEN** the location is "lucerne" and a model is "icon_d2"
- **THEN** expected entity IDs follow HA naming: `weather.lucerne_icon_d2`, `sensor.lucerne_frost_alert`, etc.
