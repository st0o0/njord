## ADDED Requirements

### Requirement: Wave peak period variable
The system SHALL request `wave_peak_period` from the Marine API when a feature requires it. `wave_peak_period` is the period at spectral peak energy for the combined wave field, distinct from `wind_wave_peak_period` and `swell_wave_peak_period`.

#### Scenario: wave_peak_period available in MarinePoint
- **WHEN** a feature declares `MarineParams.WavePeakPeriod` in its requirements
- **THEN** the Marine client SHALL include `wave_peak_period` in the API request
- **AND** the parsed `MarinePoint` SHALL contain the `WavePeakPeriod` value

#### Scenario: wave_peak_period mapped correctly
- **WHEN** the Marine API returns a `wave_peak_period` value
- **THEN** the system SHALL parse it as seconds (same unit as other period variables)

## MODIFIED Requirements

### Requirement: Marine forecast days range
The system SHALL allow `MarineOptions.ForecastDays` values from 1 to 16. The default SHALL remain 8.

#### Scenario: Operator sets forecast days up to 16
- **WHEN** the operator configures `Njord:Marine:ForecastDays` to 16
- **THEN** the validator SHALL accept the value
- **AND** the Marine client SHALL pass `forecast_days=16` to the API

#### Scenario: Operator sets forecast days above 16
- **WHEN** the operator configures `Njord:Marine:ForecastDays` to 17
- **THEN** the validator SHALL reject the value at startup
