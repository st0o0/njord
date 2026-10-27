## ADDED Requirements

### Requirement: GranularAqi provides per-pollutant AQI sensors
The system SHALL provide a `GranularAqi` feature in `AirQuality` that creates sensors for each European AQI sub-index: PM2.5, PM10, NO2, ozone, and SO2.

#### Scenario: Per-pollutant sensors created
- **WHEN** GranularAqi is enabled
- **THEN** the system SHALL create 5 sensors: `european_aqi_pm2_5`, `european_aqi_pm10`, `european_aqi_nitrogen_dioxide`, `european_aqi_ozone`, `european_aqi_sulphur_dioxide`

#### Scenario: Sub-index values
- **WHEN** the AQ API returns per-pollutant AQI values
- **THEN** each sensor SHALL report the numeric index value (0-500 scale)

### Requirement: GranularAqi config
`GranularAqiConfig` SHALL have `Enabled` (default false).

#### Scenario: Default disabled
- **WHEN** no configuration is provided
- **THEN** all 5 sub-index sensors SHALL not be created
