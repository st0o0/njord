# marine-expansion Specification

## Purpose
TBD - created by archiving change expand-marine. Update Purpose after archive.
## Requirements
### Requirement: Wave peak period variable
The system SHALL request `wave_peak_period` from the Marine API when a feature requires it. `wave_peak_period` is the period at spectral peak energy for the combined wave field, distinct from `wind_wave_peak_period` and `swell_wave_peak_period`.

#### Scenario: wave_peak_period available in MarinePoint
- **WHEN** a feature declares `MarineParams.WavePeakPeriod` in its requirements
- **THEN** the Marine client SHALL include `wave_peak_period` in the API request
- **AND** the parsed `MarinePoint` SHALL contain the `WavePeakPeriod` value

#### Scenario: wave_peak_period mapped correctly
- **WHEN** the Marine API returns a `wave_peak_period` value
- **THEN** the system SHALL parse it as seconds (same unit as other period variables)

### Requirement: Marine forecast days range
The system SHALL allow `MarineOptions.ForecastDays` values from 1 to 16. The default SHALL remain 8.

#### Scenario: Operator sets forecast days up to 16
- **WHEN** the operator configures `Njord:Marine:ForecastDays` to 16
- **THEN** the validator SHALL accept the value
- **AND** the Marine client SHALL pass `forecast_days=16` to the API

#### Scenario: Operator sets forecast days above 16
- **WHEN** the operator configures `Njord:Marine:ForecastDays` to 17
- **THEN** the validator SHALL reject the value at startup

### Requirement: Marine separates wind waves and swell
`MarineParams` and `MarinePoint` SHALL cover `wind_wave_height`, `wind_wave_period`, `wind_wave_direction`, `wind_wave_peak_period`, `swell_wave_height`, `swell_wave_period`, `swell_wave_direction`, `swell_wave_peak_period` and `sea_level_height_msl`, each as a flag and a nullable point member. The existing `WaveHeight`, `WavePeriod` and `WaveDirection` SHALL remain the combined sea state. Every API-name mapping, response mapping and `HasFields` switch SHALL have one arm per member and throw `ArgumentOutOfRangeException` for unknown values.

#### Scenario: Request contains swell variables
- **WHEN** the requested params include `SwellWaveHeight | SwellWavePeriod`
- **THEN** the marine request SHALL contain `swell_wave_height` and `swell_wave_period` in `hourly` and SHALL still be a single call

#### Scenario: Response values are mapped
- **WHEN** the response contains `swell_wave_height` and `wind_wave_height` series
- **THEN** each mapped `MarinePoint` SHALL carry both values separately and the combined `WaveHeight` SHALL be unchanged

#### Scenario: Missing swell data
- **WHEN** the response contains no swell values for an hour
- **THEN** the swell members of that point SHALL be null and `HasFields(SwellWaveHeight)` SHALL return false

### Requirement: Surf conditions enrichment
The system SHALL provide a `SurfConditions` enrichment (toggle default false) that evaluates swell quality from swell period, swell height and, when Weather wind direction is available, wind relative to swell direction (offshore, cross, onshore), and emits a lifestyle event with a score, a rating and its key values over the existing lifestyle wire. It SHALL trigger the Marine domain.

#### Scenario: Long-period moderate swell with offshore wind
- **WHEN** swell period is above 12 s, swell height is within the usable band and wind blows against the swell direction
- **THEN** the rating SHALL be the highest rating and the offshore factor SHALL be reported

#### Scenario: Short-period wind chop
- **WHEN** swell period is below 8 s and wind waves dominate
- **THEN** the rating SHALL be poor

#### Scenario: No wind data
- **WHEN** Weather wind direction is unavailable
- **THEN** the event SHALL still be produced from swell alone and SHALL indicate that wind was not considered

#### Scenario: Toggle off
- **WHEN** `SurfConditions` is disabled
- **THEN** no surf event SHALL be raised and no swell or wind-wave variable SHALL be requested

### Requirement: Existing swimming behavior is preserved
`SwimmingConditionsHandler` SHALL keep evaluating combined wave height and SHALL NOT change its output because of this change.

#### Scenario: Swimming alert unchanged
- **WHEN** the same combined wave data is evaluated before and after this change
- **THEN** the swimming alert SHALL be identical

### Requirement: No API-budget change
Adding marine variables SHALL NOT change the number of marine calls per location per poll cycle.

#### Scenario: Budget projection unchanged
- **WHEN** all new flags are requested
- **THEN** the `BudgetGuard` projection for the Marine domain SHALL equal the projection without them

