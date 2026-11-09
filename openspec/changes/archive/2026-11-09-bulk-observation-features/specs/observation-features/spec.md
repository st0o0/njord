## MODIFIED Requirements

### Requirement: Sample observation features for validation
The system SHALL provide observation features for commonly useful domain sensor values. At minimum, the following domains SHALL be covered: AirQuality (pollutant concentrations and AQI), Marine (water temperature, wave metrics, currents), Forecast (dew point, wind gusts, soil, snow, radiation, visibility), and Flood (discharge). Each observation feature follows the established pattern: a trivial `FeatureRule` that reads one field from the domain point and returns an `ObservationResult` with the descriptor name as `FeatureName`.

#### Scenario: Multiple domains covered
- **WHEN** the observation feature set is registered
- **THEN** at least four distinct `DomainKind` values SHALL be represented among observation features

#### Scenario: AirQuality observations available
- **WHEN** the AirQuality domain is fetched and contains pollutant values
- **THEN** observation features SHALL exist for PM2.5, PM10, Ozone, NitrogenDioxide, EuropeanAqi, CarbonMonoxide, and SulphurDioxide

#### Scenario: Marine observations available
- **WHEN** the Marine domain is fetched and contains wave and current data
- **THEN** observation features SHALL exist for WaveHeight, WaterTemperature, WavePeriod, WaveDirection, and OceanCurrentVelocity

#### Scenario: Forecast observations available
- **WHEN** the Forecast domain is fetched and contains sensor-grade values not in WeatherResult
- **THEN** observation features SHALL exist for UvIndex, DewPoint, WindGusts, SoilTemperature, SoilMoisture, SnowDepth, Evapotranspiration, ShortwaveRadiation, and Visibility

#### Scenario: Flood observations available
- **WHEN** the Flood domain is fetched and contains discharge data
- **THEN** observation features SHALL exist for Discharge and DischargeMean

#### Scenario: New observations are disabled by default
- **WHEN** no explicit configuration enables a new observation feature
- **THEN** the feature SHALL be disabled
