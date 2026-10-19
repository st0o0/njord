## ADDED Requirements

### Requirement: Ensemble request includes solar radiation variables
The Ensemble Mean API request SHALL include `shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, and `sunshine_duration` in the hourly variable list.

#### Scenario: Solar variables in request URL
- **WHEN** the Ensemble request URL is built
- **THEN** it SHALL include the five solar radiation variable names

### Requirement: WeatherPoint carries solar data
WeatherPoint SHALL include optional fields for shortwave radiation, direct radiation, diffuse radiation, direct normal irradiance, and sunshine duration parsed from the Ensemble response.

#### Scenario: Solar fields populated from Ensemble response
- **WHEN** the Ensemble response contains solar radiation columns
- **THEN** the resulting WeatherPoint SHALL have the corresponding fields populated
