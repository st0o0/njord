## MODIFIED Requirements

### Requirement: Ensemble spread variables
The system SHALL request spread variables from the Ensemble Mean API for the primary model. The spread variable set SHALL include at minimum: `temperature_2m_spread`, `precipitation_spread`, `wind_speed_10m_spread`, `pressure_msl_spread`, `cloud_cover_spread`, `wind_gusts_10m_spread`, `cape_spread`, `snowfall_spread`, `rain_spread`, `dew_point_2m_spread`, `relative_humidity_2m_spread`, `surface_pressure_spread`, `wind_direction_10m_spread`, `wet_bulb_temperature_2m_spread`, `wind_speed_100m_spread`.

#### Scenario: Spread variables requested from API
- **WHEN** a feature declares any spread requirement in `DomainRequirements.AllEnsemble`
- **THEN** the Ensemble client SHALL include the corresponding `_spread` variable in the API request

#### Scenario: Spread value parsed into EnsemblePoint
- **WHEN** the API returns `pressure_msl_spread_ecmwf_ifs025_ensemble_mean` with data
- **THEN** the parsed `EnsemblePoint` SHALL contain a non-null `PressureSpread` value

#### Scenario: Unavailable spread returns null
- **WHEN** the API returns a spread variable with all null values (model does not support it)
- **THEN** the parsed `EnsemblePoint` SHALL have that spread field as null
