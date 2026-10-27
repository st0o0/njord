# domain-point-types Specification

## Purpose

Domain-specific forecast point types for each Open-Meteo API endpoint: WeatherPoint, EnsemblePoint, AirQualityPoint, FloodPoint, SolarPoint, and MarinePoint. Each implements IHasValidTime for temporal correlation across domains.

## Requirements

### Requirement: Domain point types implement IHasValidTime
Each domain point type (WeatherPoint, EnsemblePoint, AirQualityPoint, FloodPoint, SolarPoint, MarinePoint) SHALL implement IHasValidTime with a ValidFrom property.

#### Scenario: WeatherPoint has valid time
- **WHEN** a WeatherPoint is created from a Forecast API hourly data point
- **THEN** its ValidFrom SHALL be the UTC timestamp of that hourly slot

### Requirement: WeatherPoint carries ensemble statistics
WeatherPoint SHALL include mean values for all weather parameters. WeatherPoint SHALL NOT contain `EnsembleStdDev`, `PrecipitationSpread`, or `WindSpeedSpread` -- these fields move to `EnsemblePoint`.

#### Scenario: Ensemble mean and std dev
- **WHEN** a WeatherPoint is created from the Forecast API
- **THEN** it SHALL carry the deterministic forecast values and SHALL NOT have spread fields

### Requirement: EnsemblePoint carries spread fields
The system SHALL provide an `EnsemblePoint` record implementing `IHasValidTime` with `ValidFrom`, `TemperatureSpread`, `PrecipitationSpread`, and `WindSpeedSpread` -- all nullable doubles. A lookup method SHALL return the spread at a given timestamp for use by alert rules.

#### Scenario: EnsemblePoint created from Ensemble Mean API
- **WHEN** the Ensemble Mean API returns `temperature_2m_spread` for an hourly slot
- **THEN** `EnsemblePoint.TemperatureSpread` SHALL carry that value at the matching `ValidFrom`

#### Scenario: Spread lookup by timestamp
- **WHEN** an alert rule looks up spread at a WeatherPoint's ValidFrom
- **THEN** the lookup SHALL return the EnsemblePoint with the closest matching timestamp, or null if no ensemble data exists

### Requirement: EnsembleParams flags enum
The system SHALL provide an `EnsembleParams` flags enum with members for each spread variable (`TemperatureSpread`, `PrecipitationSpread`, `WindSpeedSpread`) and a `Models` member for comparison model data.

#### Scenario: Feature requires only temperature spread
- **WHEN** a feature declares `Ensemble: EnsembleParams.TemperatureSpread`
- **THEN** the ensemble request SHALL include `temperature_2m_spread`

#### Scenario: ModelComparison requires Models
- **WHEN** the ModelComparison feature is enabled
- **THEN** it SHALL declare `Ensemble: EnsembleParams.Models`

### Requirement: WeatherPoint carries new atmospheric and soil variables
WeatherPoint SHALL include fields for `Showers`, `SoilMoisture0To1cm`, `SoilMoisture1To3cm`, `Evapotranspiration`, `VapourPressureDeficit`, `BoundaryLayerHeight`, `CloudCoverLow`, `CloudCoverMid`, `CloudCoverHigh`, `ConvectiveInhibition`, and `LiftedIndex` -- all nullable doubles.

#### Scenario: Showers field
- **WHEN** the Forecast API returns `showers` data
- **THEN** `WeatherPoint.Showers` SHALL carry the convective precipitation value

#### Scenario: Soil moisture fields
- **WHEN** the Forecast API returns `soil_moisture_0_to_1cm`
- **THEN** `WeatherPoint.SoilMoisture0To1cm` SHALL carry the volumetric moisture value in m3/m3

#### Scenario: Boundary layer height field
- **WHEN** the Forecast API returns `boundary_layer_height`
- **THEN** `WeatherPoint.BoundaryLayerHeight` SHALL carry the mixing layer depth in meters

### Requirement: AirQualityPoint carries new pollutant and pollen variables
AirQualityPoint SHALL include fields for `RagweedPollen`, `EuropeanAqiPm25`, `EuropeanAqiPm10`, `EuropeanAqiNitrogenDioxide`, `EuropeanAqiOzone`, `EuropeanAqiSulphurDioxide`, and `AerosolOpticalDepth` -- all nullable doubles.

#### Scenario: Ragweed pollen field
- **WHEN** the AQ API returns `ragweed_pollen` data
- **THEN** `AirQualityPoint.RagweedPollen` SHALL carry the concentration in grains/m3

#### Scenario: Granular AQI fields
- **WHEN** the AQ API returns `european_aqi_pm2_5`
- **THEN** `AirQualityPoint.EuropeanAqiPm25` SHALL carry the sub-index value

### Requirement: MarinePoint carries ocean current variables
MarinePoint SHALL include fields for `OceanCurrentVelocity` and `OceanCurrentDirection` -- both nullable doubles.

#### Scenario: Ocean current fields
- **WHEN** the Marine API returns `ocean_current_velocity` and `ocean_current_direction`
- **THEN** `MarinePoint.OceanCurrentVelocity` SHALL carry km/h and `OceanCurrentDirection` SHALL carry degrees

### Requirement: WeatherPoint carries clear-sky UV index
WeatherPoint SHALL include an optional `UvIndexClearSky` field (`double?`). The field SHALL be populated from the Forecast API's `uv_index_clear_sky` variable and SHALL be gated by `WeatherParams.UvIndexClearSky` in `HasFields`.

#### Scenario: Clear-sky UV present
- **WHEN** the Forecast API returns `uv_index_clear_sky` data
- **THEN** `WeatherPoint.UvIndexClearSky` SHALL carry the value

#### Scenario: Clear-sky UV absent
- **WHEN** no feature requires `UvIndexClearSky`
- **THEN** the variable SHALL not be requested and the field SHALL be null

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
MarinePoint SHALL include optional fields for secondary swell (height, direction, period), tertiary swell (height, direction, period), and `InvertBarometerHeight`. Each field SHALL be nullable double with a corresponding `MarineParams` flag.

#### Scenario: Secondary swell available
- **WHEN** a MarinePoint is created from a response containing `secondary_swell_wave_height`
- **THEN** `SecondarySwellWaveHeight` SHALL carry the value in meters

#### Scenario: Tertiary swell absent for non-GFS models
- **WHEN** the marine API response does not contain tertiary swell fields
- **THEN** the tertiary swell fields on MarinePoint SHALL be null
