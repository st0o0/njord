## ADDED Requirements

### Requirement: WeatherConditionMapper translates WMO codes to HA conditions
A static `WeatherConditionMapper` SHALL map WMO weather interpretation codes (0-99) to HA-compatible condition strings. The mapping SHALL consider the `is_day` flag to distinguish `sunny` from `clear-night`.

#### Scenario: Clear sky during day
- **WHEN** weather_code is 0 and is_day is true
- **THEN** the condition SHALL be `"sunny"`

#### Scenario: Clear sky at night
- **WHEN** weather_code is 0 and is_day is false
- **THEN** the condition SHALL be `"clear-night"`

#### Scenario: Partly cloudy
- **WHEN** weather_code is 1 or 2
- **THEN** the condition SHALL be `"partlycloudy"`

#### Scenario: Overcast
- **WHEN** weather_code is 3
- **THEN** the condition SHALL be `"cloudy"`

#### Scenario: Fog
- **WHEN** weather_code is 45 or 48
- **THEN** the condition SHALL be `"fog"`

#### Scenario: Rain
- **WHEN** weather_code is in 51-65 or 80-82
- **THEN** the condition SHALL be `"rainy"` for light/moderate, `"pouring"` for heavy (codes 65, 82)

#### Scenario: Snow
- **WHEN** weather_code is in 71-77 or 85-86
- **THEN** the condition SHALL be `"snowy"`

#### Scenario: Mixed rain and snow
- **WHEN** weather_code is 66 or 67
- **THEN** the condition SHALL be `"snowy-rainy"`

#### Scenario: Thunderstorm
- **WHEN** weather_code is in 95-99
- **THEN** the condition SHALL be `"lightning-rainy"`

#### Scenario: Unknown code defaults to exceptional
- **WHEN** weather_code is not in any mapped range
- **THEN** the condition SHALL be `"exceptional"`
