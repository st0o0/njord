## Purpose

Defines how WMO weather codes are translated to Home Assistant condition strings, handling day/night variants and unknown codes.

## Requirements

### Requirement: WMO to HA condition mapping
The integration SHALL map WMO weather codes combined with the is_day flag to Home Assistant condition strings.

#### Scenario: Clear sky day
- **WHEN** weather_code is 0 and is_day is True
- **THEN** the mapped condition is `sunny`

#### Scenario: Clear sky night
- **WHEN** weather_code is 0 and is_day is False
- **THEN** the mapped condition is `clear-night`

#### Scenario: Overcast
- **WHEN** weather_code is 3
- **THEN** the mapped condition is `cloudy` regardless of is_day

#### Scenario: Rain
- **WHEN** weather_code is 61 (slight rain)
- **THEN** the mapped condition is `rainy`

#### Scenario: Thunderstorm
- **WHEN** weather_code is 95
- **THEN** the mapped condition is `lightning-rainy`

#### Scenario: Unknown code
- **WHEN** weather_code is not in the mapping table
- **THEN** the mapped condition is `exceptional`
