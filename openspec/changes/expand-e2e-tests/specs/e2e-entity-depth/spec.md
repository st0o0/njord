## ADDED Requirements

### Requirement: Weather entity attribute validation

The E2E test plan SHALL validate the full attribute set of weather entities, not just existence.

#### Scenario: Weather entity state is a valid condition
- **WHEN** `GET /api/states/weather.lucerne_icon_d2` is called
- **THEN** the state is one of the HA weather conditions (e.g., "sunny", "cloudy", "rainy", "partlycloudy", "snowy", "fog", "windy", "lightning", "clear-night", "exceptional") — never "unavailable" or "unknown"

#### Scenario: Weather entity has required numeric attributes
- **WHEN** a weather entity's attributes are inspected
- **THEN** `temperature`, `humidity`, `pressure`, `wind_speed`, and `wind_bearing` are present and numeric

#### Scenario: Weather entity has correct temperature unit
- **WHEN** a weather entity's attributes are inspected
- **THEN** `temperature_unit` is "°C"

#### Scenario: Weather entity has correct wind speed unit
- **WHEN** a weather entity's attributes are inspected
- **THEN** `wind_speed_unit` is "m/s" (njord sends wind in m/s)

### Requirement: Alert sensor attribute validation

The E2E test plan SHALL validate alert sensor attributes beyond existence.

#### Scenario: Alert sensor has severity attribute
- **WHEN** any alert sensor (e.g., `sensor.lucerne_frost_alert`) is inspected
- **THEN** the `severity` attribute is present and is one of "none", "low", "medium", "high", "extreme"

#### Scenario: Alert sensor has confidence attribute
- **WHEN** any alert sensor is inspected
- **THEN** the `confidence` attribute is present and is a numeric value between 0 and 100

#### Scenario: Alert sensor has correct device_class
- **WHEN** an alert sensor's attributes are inspected
- **THEN** `device_class` is absent or appropriate (alert sensors are generic sensors, not temperature/humidity)

### Requirement: Index sensor attribute validation

The E2E test plan SHALL validate index sensor values are within expected ranges.

#### Scenario: Index sensor state is numeric
- **WHEN** any index sensor (e.g., `sensor.lucerne_laundry_index`) is inspected
- **THEN** the state is a numeric value (integer or float)

#### Scenario: Index sensor value is within bounds
- **WHEN** an index sensor's state is inspected
- **THEN** the value is between 0 and 10 (inclusive) for standard indices, or within the documented range for special indices (VPD, frost_hours)

### Requirement: Derived sensor attribute validation

The E2E test plan SHALL validate derived sensor values and units.

#### Scenario: Beaufort sensor is integer 0-12
- **WHEN** `sensor.lucerne_beaufort` is inspected
- **THEN** the state is an integer between 0 and 12

#### Scenario: Sunshine sensor has hours unit
- **WHEN** `sensor.lucerne_sunshine` is inspected
- **THEN** the `unit_of_measurement` attribute is "h" and the state is numeric ≥ 0

#### Scenario: Dewpoint comfort has descriptive state
- **WHEN** `sensor.lucerne_dewpoint_comfort` is inspected
- **THEN** the state is a descriptive string (e.g., "comfortable", "humid", "oppressive")

### Requirement: Server sensor attribute validation

The E2E test plan SHALL validate server sensor values and units.

#### Scenario: Version sensor matches njord version
- **WHEN** `sensor.version` is inspected
- **THEN** the state is a non-empty version string matching semver pattern (e.g., "0.x.y" or "1.x.y")

#### Scenario: Usage sensors have correct units
- **WHEN** `sensor.monthly_usage` or `sensor.daily_usage` is inspected
- **THEN** the state is a non-negative integer and the `unit_of_measurement` is "requests"

#### Scenario: Uptime sensor has duration value
- **WHEN** `sensor.uptime` is inspected
- **THEN** the state is a non-empty duration or timestamp value

### Requirement: Binary sensor state validation

The E2E test plan SHALL validate binary sensor states after a successful poll.

#### Scenario: Stream sensors are on after poll
- **WHEN** a poll cycle has completed and stream sensors are inspected
- **THEN** `binary_sensor.forecast_stream`, `binary_sensor.enrichment_stream`, and `binary_sensor.config_stream` all have state "on"

#### Scenario: Inversion sensor has boolean state
- **WHEN** `binary_sensor.lucerne_inversion` is inspected
- **THEN** the state is "on" or "off"
