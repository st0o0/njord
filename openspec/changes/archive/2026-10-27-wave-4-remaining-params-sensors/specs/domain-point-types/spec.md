## ADDED Requirements

### Requirement: WeatherPoint carries elevated and surface variables
WeatherPoint SHALL include optional fields for `SurfacePressure`, `WindSpeed80m`, `WindDirection80m`, `WindSpeed120m`, `WindDirection120m`, `Temperature80m`, and `Temperature120m`. Each field SHALL be nullable double with a corresponding `WeatherParams` flag.

#### Scenario: Surface pressure available
- **WHEN** a WeatherPoint is created from a Forecast API response containing `surface_pressure`
- **THEN** `SurfacePressure` SHALL carry the value in hPa

#### Scenario: Elevated wind available
- **WHEN** a WeatherPoint is created from a response containing `wind_speed_80m`
- **THEN** `WindSpeed80m` SHALL carry the value in m/s

#### Scenario: HasFields checks new flags
- **WHEN** `HasFields` is called with `WeatherParams.WindSpeed80m`
- **THEN** it SHALL return true only when `WindSpeed80m` is not null

### Requirement: AirQualityPoint carries gas and US AQI variables
AirQualityPoint SHALL include optional fields for `CarbonDioxide`, `Methane`, `UsAqiPm25`, `UsAqiPm10`, `UsAqiNitrogenDioxide`, `UsAqiOzone`, `UsAqiSulphurDioxide`, and `UsAqiCarbonMonoxide`. Each field SHALL be nullable double with a corresponding `AirQualityParams` flag.

#### Scenario: CO2 available
- **WHEN** an AirQualityPoint is created from a response containing `carbon_dioxide`
- **THEN** `CarbonDioxide` SHALL carry the value in ppm

#### Scenario: US AQI sub-index available
- **WHEN** an AirQualityPoint is created from a response containing `us_aqi_pm2_5`
- **THEN** `UsAqiPm25` SHALL carry the index value

### Requirement: MarinePoint carries secondary and tertiary swell variables
MarinePoint SHALL include optional fields for `SecondarySwell` (height, direction, period), `TertiarySwell` (height, direction, period), and `InvertBarometerHeight`. Each field SHALL be nullable double with a corresponding `MarineParams` flag.

#### Scenario: Secondary swell available
- **WHEN** a MarinePoint is created from a response containing `secondary_swell_wave_height`
- **THEN** `SecondarySwell.Height` (or `SecondarySwellWaveHeight`) SHALL carry the value in meters

#### Scenario: Tertiary swell absent for non-GFS models
- **WHEN** the marine API response does not contain tertiary swell fields
- **THEN** the tertiary swell fields on MarinePoint SHALL be null
