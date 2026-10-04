## Purpose

Provides an `njord.push_sensor` HA service for manual or automation-driven sensor pushes to njord's SensorService.

## Requirements

### Requirement: push_sensor service registration
The integration SHALL register a domain service `njord.push_sensor` on setup. The service SHALL be removed on unload when no entries remain.

#### Scenario: Service available after setup
- **WHEN** the njord integration is set up
- **THEN** `njord.push_sensor` is available as a service

#### Scenario: Service removed on last unload
- **WHEN** the last njord config entry is unloaded
- **THEN** `njord.push_sensor` is no longer available

### Requirement: push_sensor service parameters
The service SHALL accept: `kind` (required, string — one of `indoor_temperature`, `indoor_humidity`), `entity_id` (required, string — the HA entity to read the value from), `location` (optional, string), `source` (optional, string — defaults to entity_id).

#### Scenario: All parameters provided
- **WHEN** the service is called with `kind: indoor_temperature`, `entity_id: sensor.wz_temp`, `location: home`, `source: wohnzimmer`
- **THEN** `client.push_sensor("indoor_temperature", "home", <current value>, source="wohnzimmer")` is called

#### Scenario: Source defaults to entity_id
- **WHEN** the service is called without `source`
- **THEN** the entity_id is used as the source

### Requirement: Location auto-resolve
If `location` is omitted, the service SHALL auto-resolve it when the catalog contains exactly one location. If the catalog contains multiple locations and `location` is omitted, the service SHALL raise a `ServiceValidationError`.

#### Scenario: Single location auto-resolved
- **WHEN** the catalog has one location "home" and the service is called without `location`
- **THEN** "home" is used as the location

#### Scenario: Multiple locations require explicit location
- **WHEN** the catalog has locations "home" and "büro" and the service is called without `location`
- **THEN** a `ServiceValidationError` is raised indicating location is required

### Requirement: Value read from entity state
The service SHALL read the current state of the specified `entity_id` and convert it to a float. If the entity does not exist or its state is not a valid float, the service SHALL raise a `ServiceValidationError`.

#### Scenario: Valid entity state
- **WHEN** the service is called with `entity_id: sensor.wz_temp` and the entity's state is "22.5"
- **THEN** the value 22.5 is pushed to njord

#### Scenario: Entity not found
- **WHEN** the service is called with a non-existent entity_id
- **THEN** a `ServiceValidationError` is raised

#### Scenario: Non-numeric state
- **WHEN** the entity's state is "unavailable"
- **THEN** a `ServiceValidationError` is raised

### Requirement: Invalid kind rejected
The service SHALL validate that `kind` is one of the supported sensor kinds. If not, a `ServiceValidationError` SHALL be raised.

#### Scenario: Unknown kind
- **WHEN** the service is called with `kind: outdoor_pressure`
- **THEN** a `ServiceValidationError` is raised
