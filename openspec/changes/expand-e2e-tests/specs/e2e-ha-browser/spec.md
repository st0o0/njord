## ADDED Requirements

### Requirement: HA entity card verification via browser

The E2E test plan SHALL verify that njord entities render correctly in the HA web UI.

#### Scenario: Weather entity card displays current conditions
- **WHEN** the HA dashboard is opened in the browser and a weather entity card for `weather.lucerne_icon_d2` is visible
- **THEN** the card shows temperature, humidity, wind speed, and a weather condition icon

#### Scenario: Sensor entity displays numeric value
- **WHEN** a sensor entity (e.g., `sensor.lucerne_laundry_index`) is viewed in the HA entity list or developer tools
- **THEN** the entity shows its numeric state value and unit of measurement

#### Scenario: Binary sensor shows on/off state
- **WHEN** `binary_sensor.forecast_stream` is viewed in HA developer tools → States
- **THEN** the entity shows state "on" or "off" with appropriate icon

### Requirement: HA Developer Tools entity inspection

The E2E test plan SHALL use HA Developer Tools to verify entity attributes in the browser.

#### Scenario: Entity attributes visible in Developer Tools
- **WHEN** the HA Developer Tools → States page is opened and an entity (e.g., `weather.lucerne_consensus`) is selected
- **THEN** the entity's full attribute list is visible including custom attributes (agreement, spread, models_used)

#### Scenario: Entity filter works for njord entities
- **WHEN** "njord" or "lucerne" is typed in the Developer Tools state filter
- **THEN** only njord-related entities are shown and the count matches the expected entity grid

### Requirement: Trigger poll from HA UI

The E2E test plan SHALL verify that the trigger_poll button works from the HA interface.

#### Scenario: Button press via HA UI
- **WHEN** `button.trigger_poll` is found in the HA UI (entity page or developer tools) and pressed
- **THEN** within 60 seconds, weather entity `last_updated` timestamps are refreshed
