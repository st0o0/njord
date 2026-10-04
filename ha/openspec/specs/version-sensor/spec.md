## Purpose

Exposes the njord server version as a dedicated diagnostic sensor, replacing the version attribute previously embedded in the uptime sensor.

## Requirements

### Requirement: Version sensor shows njord container version
A `sensor.server_version` entity SHALL display the njord server version string from `GetStatus().version` as its state.

#### Scenario: Version displayed
- **WHEN** `ServerStatusData` has `version="1.2.3"`
- **THEN** the sensor's `native_value` is `"1.2.3"`

#### Scenario: Status unavailable
- **WHEN** the status coordinator has no data
- **THEN** the sensor is unavailable

### Requirement: Version sensor is diagnostic on the Server device
The version sensor SHALL have `entity_category=DIAGNOSTIC`, belong to the Server device, and use `NjordStatusCoordinator`.

#### Scenario: Sensor metadata
- **WHEN** the integration is set up
- **THEN** the version sensor is registered as a diagnostic entity on the Server device

### Requirement: Uptime sensor no longer exposes version attribute
`NjordUptimeSensor` SHALL NOT include `version` in its `extra_state_attributes`.

#### Scenario: Uptime has no version attribute
- **WHEN** the uptime sensor is displayed
- **THEN** `extra_state_attributes` does not contain `version`
