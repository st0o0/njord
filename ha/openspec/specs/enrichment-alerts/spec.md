### Requirement: Inversion sensor is disabled by default

The inversion binary sensor SHALL have `_attr_entity_registry_enabled_default = False`.

#### Scenario: Inversion sensor is disabled after setup

- **WHEN** the integration is set up for the first time
- **THEN** the inversion binary sensor is registered but disabled
