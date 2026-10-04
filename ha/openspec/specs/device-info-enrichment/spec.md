## Purpose

Ensures all HA device registry entries include server version and device model type, and that button entities use consistent DeviceInfo with server sensors.

## Requirements

### Requirement: All DeviceInfo entries include sw_version
Every `DeviceInfo` created by the integration SHALL include `sw_version` populated from the server version obtained via `GetStatus()`. If the status coordinator is unavailable, `sw_version` SHALL be omitted (not set to a placeholder).

#### Scenario: Server version available at entity creation
- **WHEN** entities are created and the status coordinator has data with `version = "2.1.0"`
- **THEN** all DeviceInfo entries include `sw_version="2.1.0"`

#### Scenario: Status coordinator unavailable at entity creation
- **WHEN** entities are created but the status coordinator failed its first refresh
- **THEN** DeviceInfo entries omit `sw_version` (field not set)

### Requirement: All DeviceInfo entries include model
Every `DeviceInfo` SHALL include a `model` field. Location-scoped devices SHALL use `model="Weather Station"`. The server-scoped device SHALL use `model="Weather Service"`.

#### Scenario: Location device model
- **WHEN** a weather or enrichment entity creates its DeviceInfo for location "Innsbruck"
- **THEN** `model="Weather Station"` is set

#### Scenario: Server device model
- **WHEN** a server-level entity (budget, uptime, version, trigger poll) creates its DeviceInfo
- **THEN** `model="Weather Service"` is set

### Requirement: Button entity uses shared server DeviceInfo
ALL platforms (weather, sensor, binary_sensor, event, button) SHALL construct DeviceInfo via shared helper functions (`device_info()` for location-scoped devices, `server_device_info()` for the server device). These helpers SHALL live in a shared module (`helpers.py`), not in any single platform file. No platform SHALL construct DeviceInfo inline.

#### Scenario: Button device groups with server sensors
- **WHEN** the trigger poll button and server sensors are registered
- **THEN** they appear under the same device in HA's device registry
- **AND** the device shows manufacturer, model, and sw_version

#### Scenario: Binary sensor uses shared location DeviceInfo
- **WHEN** an inversion binary sensor is created for location "innsbruck"
- **THEN** it uses `device_info(entry, "innsbruck", sw_version)` from the shared helpers module
- **AND** the resulting DeviceInfo is identical to what weather and sensor entities produce for the same location

#### Scenario: Event entity uses shared location DeviceInfo
- **WHEN** a weather alert event entity is created for location "innsbruck"
- **THEN** it uses `device_info(entry, "innsbruck", sw_version)` from the shared helpers module

#### Scenario: Weather entity uses shared location DeviceInfo
- **WHEN** a weather entity is created for location "innsbruck" with model "icon-d2"
- **THEN** it uses `device_info(entry, "innsbruck", sw_version)` from the shared helpers module

#### Scenario: Stream sensor uses shared server DeviceInfo
- **WHEN** a stream health binary sensor is created
- **THEN** it uses `server_device_info(entry, sw_version)` from the shared helpers module
