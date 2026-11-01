## ADDED Requirements

### Requirement: Thunderstorm probability variable
The system SHALL support `thunderstorm_probability` as a requestable Forecast variable. This is a direct probability (0-100%) from the GFS National Blend of Models (NBM), available only with the `ncep_nbm_conus` model or `best_match` for US locations.

#### Scenario: thunderstorm_probability in ForecastParams
- **WHEN** a feature declares `ForecastParams.ThunderstormProbability` in its requirements
- **THEN** the Forecast client SHALL include `thunderstorm_probability` in the API request
- **AND** the parsed `ForecastPoint` SHALL contain the `ThunderstormProbability` value as a percentage (0-100)

#### Scenario: thunderstorm_probability null for non-NBM models
- **WHEN** the API returns null values for `thunderstorm_probability` (model does not support it)
- **THEN** the parsed `ForecastPoint` SHALL have `ThunderstormProbability` as null
