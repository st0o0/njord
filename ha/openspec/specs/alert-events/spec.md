## Purpose

Defines the alert event platform that fires HA events on alert state transitions (started, escalated, de-escalated, cleared) instead of exposing alerts as continuous sensor entities.

## Requirements

### Requirement: Alert event entity per location
The integration SHALL create one `event` entity per location using the HA `event` platform. The entity SHALL have `unique_id = "{entry_id}_{location}_weather_alert"` and be grouped under the location's device.

#### Scenario: Event entity exists for each location
- **WHEN** the integration is set up with locations "munich" and "zurich"
- **THEN** two event entities exist: `event.njord_munich_weather_alert` and `event.njord_zurich_weather_alert`

### Requirement: Alert event fires on new alert
The entity SHALL fire an event when a new alert appears in the enrichment data (alert type was not present before or had severity "none").

#### Scenario: Frost alert appears
- **WHEN** enrichment stream delivers a frost alert with severity "yellow" and no frost alert existed before
- **THEN** the entity fires an event with `event_type = "alert_started"` and data `{type: "frost", severity: "yellow", confidence: 0.8, trigger_value: -1.2, threshold: 0.0, hours_until: 6, duration_hours: 4}`

### Requirement: Alert event fires on severity change
The entity SHALL fire an event when an existing alert's severity changes.

#### Scenario: Severity escalates
- **WHEN** a frost alert changes from severity "yellow" to "orange"
- **THEN** the entity fires an event with `event_type = "alert_escalated"` and data including the new severity and previous severity

#### Scenario: Severity de-escalates
- **WHEN** a storm alert changes from severity "red" to "yellow"
- **THEN** the entity fires an event with `event_type = "alert_deescalated"` and data including the new and previous severity

### Requirement: Alert event fires on alert cleared
The entity SHALL fire an event when an alert disappears (was present, now absent or severity becomes "none").

#### Scenario: Frost alert clears
- **WHEN** a frost alert that had severity "yellow" is no longer present in enrichment data
- **THEN** the entity fires an event with `event_type = "alert_cleared"` and data `{type: "frost", previous_severity: "yellow"}`

### Requirement: Event entity tracks previous alert state
The entity SHALL maintain a mapping of the last known alert states (type -> severity) to detect transitions. This state SHALL be initialized from the first enrichment data received.

#### Scenario: Initial state from first enrichment
- **WHEN** the entity receives its first enrichment update with frost (yellow) and storm (orange) alerts
- **THEN** no events fire (initial state, not a transition)
- **AND** subsequent changes relative to this baseline fire events

### Requirement: Event entity has device class and icon
The event entity SHALL use icon `mdi:weather-lightning-rainy` and belong to the location's device.

#### Scenario: Entity metadata
- **WHEN** the event entity is displayed in HA
- **THEN** it shows icon `mdi:weather-lightning-rainy` and is grouped under the location device

### Requirement: Alert event entity supports dynamic addition
The event platform SHALL register a factory on the coordinator for creating event entities for locations discovered after initial setup.

#### Scenario: Late event entity creation
- **WHEN** a new location "bern" is detected via config stream
- **THEN** an event entity is created for "bern"

### Requirement: Event types are declared
The event entity SHALL declare all possible event types: `alert_started`, `alert_escalated`, `alert_deescalated`, `alert_cleared`.

#### Scenario: HA knows valid event types
- **WHEN** HA queries the event entity's event types
- **THEN** it returns `["alert_started", "alert_escalated", "alert_deescalated", "alert_cleared"]`
