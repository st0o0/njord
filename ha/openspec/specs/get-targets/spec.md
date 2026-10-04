## Purpose

Consumes the `OpsService.GetTargets()` endpoint and exposes per-location/model poll state as diagnostic sensors.

## Requirements

### Requirement: gRPC client implements GetTargets
`NjordClient` SHALL implement an `async get_targets()` method that calls `OpsService.GetTargets()` and returns a list of `TargetData` dataclasses.

#### Scenario: GetTargets returns target list
- **WHEN** `get_targets()` is called and the server has 2 locations × 3 models
- **THEN** 6 `TargetData` objects are returned with location, model, last poll time, and status

#### Scenario: GetTargets server error
- **WHEN** `get_targets()` is called and the server returns an error
- **THEN** the method raises an appropriate exception

### Requirement: TargetData model
A `TargetData` frozen dataclass SHALL be added to `models.py` with fields: `location` (str), `model` (str), `last_poll` (datetime | None), `status` (str).

#### Scenario: TargetData creation
- **WHEN** a target proto message has location="Innsbruck", model="icon_d2", last_poll timestamp, status="ok"
- **THEN** a `TargetData` instance is created with matching fields

### Requirement: Status coordinator fetches targets alongside status
The `NjordStatusCoordinator` SHALL call `get_targets()` in its `_async_update_data` method alongside `get_status()`. The combined data SHALL be stored in an extended `ServerStatusData` that includes a `targets: list[TargetData]` field.

#### Scenario: Status update includes targets
- **WHEN** the status coordinator polls successfully
- **THEN** `coordinator.data.targets` contains the current target list

#### Scenario: GetTargets fails but GetStatus succeeds
- **WHEN** `get_targets()` raises an error but `get_status()` succeeds
- **THEN** the coordinator uses an empty targets list and does not fail the update

### Requirement: Diagnostic sensors for each target
A diagnostic sensor SHALL be created for each target returned by `GetTargets()`. The sensor's `native_value` SHALL be the last poll timestamp (as ISO string). Extra state attributes SHALL include `status` and `model`.

#### Scenario: Target sensor shows last poll time
- **WHEN** a target has `last_poll = 2025-01-15T10:30:00Z` and `status = "ok"`
- **THEN** the sensor's native_value is `"2025-01-15T10:30:00+00:00"`
- **AND** extra attributes include `{"status": "ok", "model": "icon_d2"}`

#### Scenario: Target sensor with no poll data
- **WHEN** a target has `last_poll = None`
- **THEN** the sensor's native_value is `None` (unavailable)

### Requirement: Target sensors are diagnostic and disabled by default
Target sensors SHALL have `entity_category = EntityCategory.DIAGNOSTIC` and `entity_registry_enabled_default = False`. They SHALL be grouped under the server device.

#### Scenario: Target sensor defaults
- **WHEN** target sensors are registered
- **THEN** each has `entity_category` DIAGNOSTIC and is disabled by default in the entity registry
