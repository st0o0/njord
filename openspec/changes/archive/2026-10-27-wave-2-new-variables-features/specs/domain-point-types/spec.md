## ADDED Requirements

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
