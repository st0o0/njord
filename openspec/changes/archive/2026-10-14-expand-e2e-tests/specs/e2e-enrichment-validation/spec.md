## ADDED Requirements

### Requirement: Consensus computation validation

The E2E test plan SHALL verify consensus weather entity attributes reflect multi-model agreement.

#### Scenario: Consensus entity has agreement attribute
- **WHEN** `weather.lucerne_consensus` attributes are inspected
- **THEN** the `agreement` attribute is present and is a numeric percentage (0–100)

#### Scenario: Consensus entity has spread attribute
- **WHEN** `weather.lucerne_consensus` attributes are inspected
- **THEN** the `spread` attribute is present and is a non-negative numeric value (temperature spread in °C)

#### Scenario: Consensus entity has models_used attribute
- **WHEN** `weather.lucerne_consensus` attributes are inspected
- **THEN** the `models_used` attribute is present and lists at least 2 model names

#### Scenario: Consensus temperature is between model temperatures
- **WHEN** the consensus temperature and individual model temperatures are compared
- **THEN** the consensus temperature is within the range bounded by the individual model temperatures (it is an aggregation, not an outlier)

### Requirement: Alert severity validation

The E2E test plan SHALL verify alert severity values are plausible for current conditions.

#### Scenario: At least one alert has non-none severity
- **WHEN** all 14 alert sensors are inspected after a successful poll
- **THEN** at least one alert has severity other than "none" (weather conditions virtually always trigger at least one alert type)

#### Scenario: Alert confidence correlates with severity
- **WHEN** an alert with severity "none" is inspected
- **THEN** its confidence is 0 or near-zero; an alert with severity "high" or "extreme" has confidence > 50

### Requirement: Index range validation

The E2E test plan SHALL verify index sensor values are within their documented ranges.

#### Scenario: Standard activity indices are 0–10
- **WHEN** laundry, outdoor, running, cycling, bbq, irrigation, solar, night_ventilation indices are inspected
- **THEN** each state is a numeric value between 0 and 10 (inclusive)

#### Scenario: VPD has pressure units
- **WHEN** `sensor.lucerne_vpd` is inspected
- **THEN** the state is a non-negative numeric value and `unit_of_measurement` is "kPa"

#### Scenario: Frost hours is non-negative
- **WHEN** `sensor.lucerne_frost_hours` is inspected
- **THEN** the state is a non-negative numeric value with `unit_of_measurement` "h"

#### Scenario: Frost confidence is 0–100
- **WHEN** `sensor.lucerne_frost_confidence` is inspected
- **THEN** the state is a numeric value between 0 and 100

### Requirement: Trend sensor validation

The E2E test plan SHALL verify the weather trend sensor produces a meaningful trend value.

#### Scenario: Trend sensor has descriptive state
- **WHEN** `sensor.lucerne_weather_trend` is inspected
- **THEN** the state is a non-empty descriptive string indicating a trend direction or stability

#### Scenario: Trend sensor has horizon attributes
- **WHEN** the trend sensor's attributes are inspected
- **THEN** attributes include information about the forecast horizon window used for the trend computation

### Requirement: Derived value validation

The E2E test plan SHALL verify derived sensors compute correct values from forecast data.

#### Scenario: Diurnal amplitude is positive
- **WHEN** `sensor.lucerne_diurnal_amplitude` is inspected
- **THEN** the state is a positive numeric value representing temperature range in °C

#### Scenario: Wind chill is numeric
- **WHEN** `sensor.lucerne_wind_chill` is inspected
- **THEN** the state is a numeric temperature value in °C

### Requirement: History sensor validation

The E2E test plan SHALL verify the model performance history sensor produces data.

#### Scenario: Model performance has state after first poll
- **WHEN** `sensor.lucerne_model_performance` is inspected after the first poll cycle
- **THEN** the state is not "unavailable" (history data may be limited on first run but the sensor exists and has a state)
