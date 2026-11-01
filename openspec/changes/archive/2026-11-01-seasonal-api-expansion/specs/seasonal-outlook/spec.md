## MODIFIED Requirements

### Requirement: Seasonal monthly variables
The system SHALL request additional monthly variables beyond the current anomaly set. The expanded monthly variable set SHALL include at minimum: `temperature_2m` (absolute), `snowfall`, `snow_depth`, `cloud_cover`, `wind_speed_10m`, `pressure_msl`, `sunshine_duration`, `shortwave_radiation`, `wind_gusts_10m`, `sea_surface_temperature`, in addition to the existing `temperature_2m_anomaly`, `precipitation_anomaly`, `precipitation_mean`.

#### Scenario: Absolute temperature alongside anomaly
- **WHEN** a feature declares `SeasonalParams.Temperature` in its requirements
- **THEN** the Seasonal client SHALL include `temperature_2m` in the monthly request
- **AND** the parsed `SeasonalPoint` SHALL contain the absolute temperature value alongside the anomaly

#### Scenario: Seasonal snowfall variable
- **WHEN** a feature declares `SeasonalParams.Snowfall` in its requirements
- **THEN** the Seasonal client SHALL include `snowfall` in the monthly request
- **AND** the parsed `SeasonalPoint` SHALL contain the monthly snowfall value

### Requirement: Explicit Seasonal model selection
The system SHALL explicitly configure `ecmwf_seasonal_ensemble_mean_seamless` as the default Seasonal model. This blends EC46 and SEAS5 for maximum variable coverage.

#### Scenario: Seamless model used by default
- **WHEN** the Seasonal client makes an API request without operator override
- **THEN** the request SHALL include `&models=ecmwf_seasonal_ensemble_mean_seamless`

### Requirement: Forecast months range
The system SHALL allow `SeasonalOptions.ForecastMonths` values from 1 to 7. The default SHALL remain 3.

#### Scenario: Operator sets forecast months to 7
- **WHEN** the operator configures `Njord:Seasonal:ForecastMonths` to 7
- **THEN** the validator SHALL accept the value

#### Scenario: Operator sets forecast months above 7
- **WHEN** the operator configures `Njord:Seasonal:ForecastMonths` to 8
- **THEN** the validator SHALL reject the value at startup
