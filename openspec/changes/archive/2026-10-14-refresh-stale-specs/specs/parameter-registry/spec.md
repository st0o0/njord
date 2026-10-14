## ADDED Requirements

### Requirement: Production code uses typed parameter properties
`ParameterRegistry` SHALL expose typed static properties for every parameter used in production code. No production code SHALL call `GetByApiName` with a string literal — string-based lookup is reserved for deserialization and configuration resolution paths where the name comes from external data.

#### Scenario: Hourly parameters have typed properties
- **GIVEN** the hourly parameters `rain`, `soil_temperature_0cm`, `visibility`, `wind_direction_10m`
- **WHEN** accessed via `ParameterRegistry`
- **THEN** typed properties `ParameterRegistry.Rain`, `ParameterRegistry.SoilTemperature0cm`, `ParameterRegistry.Visibility`, `ParameterRegistry.WindDirection10m` exist and return the correct `ParameterDef`

#### Scenario: Daily parameters have typed properties
- **GIVEN** the daily parameters `precipitation_sum`, `uv_index_max`, `snowfall_sum`, `temperature_2m_max`, `temperature_2m_min`, `wind_speed_10m_max`, `wind_gusts_10m_max`, `sunrise`, `sunset`, `weather_code`
- **WHEN** accessed via `ParameterRegistry`
- **THEN** typed properties exist for each and return the correct `ParameterDef`

#### Scenario: Alert rules use typed properties
- **GIVEN** `IceRule` checking rain
- **WHEN** it accesses the rain parameter
- **THEN** it uses `ParameterRegistry.Rain` (not `GetByApiName("rain")`)

#### Scenario: gRPC forecast mapping uses typed properties
- **GIVEN** `ForecastProtoMappings` building a response with daily forecast data
- **WHEN** it accesses `precipitation_sum`
- **THEN** it uses `ParameterRegistry.PrecipitationSum` (not `GetByApiName("precipitation_sum")`)

## REMOVED Requirements

### Requirement: All parameters have typed static properties
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Production code uses typed parameter properties" in this capability.
