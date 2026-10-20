## ADDED Requirements

### Requirement: Independent forecast days per domain
AirQuality, Marine and Flood SHALL each have their own configuration section with a forecast-days setting; Weather forecast days SHALL NOT affect them.

#### Scenario: Defaults
- **WHEN** no value is configured
- **THEN** AirQuality SHALL use 5, Marine 8 and Flood 92 forecast days

#### Scenario: Weather setting isolated
- **WHEN** Weather forecast days is set above 7
- **THEN** AirQuality requests SHALL still use the AirQuality setting

### Requirement: API limits enforced at startup
Each domain's forecast days SHALL be validated against its API limit when the service starts.

#### Scenario: Above limit
- **WHEN** AirQuality forecast days is 8, Marine 9 or Flood 211
- **THEN** startup SHALL fail with a message naming the setting and its maximum

#### Scenario: At limit
- **WHEN** AirQuality is 7, Marine 8 and Flood 210
- **THEN** validation SHALL succeed

#### Scenario: Below minimum
- **WHEN** a forecast-days value is 0 or negative
- **THEN** startup SHALL fail
