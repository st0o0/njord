## ADDED Requirements

### Requirement: UnitSystem enum
The system SHALL define a `UnitSystem` enum with values `Metric` and `Imperial`. `Metric` SHALL be the default.

#### Scenario: Default is Metric
- **WHEN** no `UnitSystem` is configured
- **THEN** the effective unit system is `Metric`

#### Scenario: Imperial is selectable
- **WHEN** `Njord:UnitSystem` is set to `Imperial`
- **THEN** the effective unit system is `Imperial`

### Requirement: UnitResolver maps parameters to active units
A static `UnitResolver` SHALL provide `GetUnit(ParameterDef param, UnitSystem system)` returning the unit string for the given parameter under the given unit system. For `Metric`, it SHALL return `ParameterDef.Unit` unchanged. For `Imperial`, it SHALL return the imperial equivalent according to the mapping table.

#### Scenario: Metric returns registry unit
- **WHEN** `GetUnit(temperature_2m, Metric)` is called
- **THEN** the result is `"°C"`

#### Scenario: Imperial maps temperature
- **WHEN** `GetUnit(temperature_2m, Imperial)` is called
- **THEN** the result is `"°F"`

#### Scenario: Imperial maps wind speed
- **WHEN** `GetUnit(wind_speed_10m, Imperial)` is called
- **THEN** the result is `"mph"`

#### Scenario: Imperial maps precipitation
- **WHEN** `GetUnit(precipitation, Imperial)` is called
- **THEN** the result is `"in"`

#### Scenario: Imperial maps pressure
- **WHEN** `GetUnit(pressure_msl, Imperial)` is called
- **THEN** the result is `"inHg"`

#### Scenario: Imperial maps snowfall
- **WHEN** `GetUnit(snowfall, Imperial)` is called where metric unit is `"cm"`
- **THEN** the result is `"in"`

#### Scenario: Imperial maps distance (visibility, freezing level)
- **WHEN** `GetUnit(visibility, Imperial)` is called where metric unit is `"m"`
- **THEN** the result is `"ft"`

#### Scenario: Dimensionless parameters are unchanged
- **WHEN** `GetUnit(cloud_cover, Imperial)` is called where metric unit is `"%"`
- **THEN** the result is `"%"`

#### Scenario: Snow depth maps to inches
- **WHEN** `GetUnit(snow_depth, Imperial)` is called where metric unit is `"m"`
- **THEN** the result is `"in"`

### Requirement: UnitConverter converts values for API-gap parameters
A static `UnitConverter` SHALL provide `Convert(ParameterDef param, double value, UnitSystem system)` that applies server-side conversion for parameters without Open-Meteo API unit support. For `Metric`, it SHALL return the value unchanged. For `Imperial`, it SHALL apply the conversion factor based on the parameter's metric unit.

#### Scenario: Metric is a no-op
- **WHEN** `Convert(pressure_msl, 1013.25, Metric)` is called
- **THEN** the result is `1013.25`

#### Scenario: Pressure hPa to inHg
- **WHEN** `Convert(pressure_msl, 1013.25, Imperial)` is called
- **THEN** the result is approximately `29.92` (1013.25 × 0.02953)

#### Scenario: Snowfall cm to inches
- **WHEN** `Convert(snowfall, 10.0, Imperial)` is called
- **THEN** the result is approximately `3.937` (10.0 × 0.3937)

#### Scenario: Snow depth m to inches
- **WHEN** `Convert(snow_depth, 0.5, Imperial)` is called
- **THEN** the result is approximately `19.685` (0.5 × 39.37)

#### Scenario: Visibility m to ft
- **WHEN** `Convert(visibility, 10000, Imperial)` is called
- **THEN** the result is approximately `32808` (10000 × 3.28084)

#### Scenario: API-covered parameter is not double-converted
- **WHEN** `Convert(temperature_2m, 20.0, Imperial)` is called
- **THEN** the result is `20.0` (temperature is handled by Open-Meteo API params, not server-side)

### Requirement: Enrichment normalization converts Imperial values to metric
The system SHALL provide a normalization function that converts forecast values from the active unit system back to metric before they enter the enrichment pipeline. This ensures all enrichment thresholds remain in metric units.

#### Scenario: Metric values pass through unchanged
- **WHEN** the unit system is `Metric` and a temperature value of `20.0` enters enrichment
- **THEN** the value is `20.0`

#### Scenario: Imperial temperature is normalized to Celsius
- **WHEN** the unit system is `Imperial` and a temperature value of `68.0` (°F) enters enrichment
- **THEN** the value is normalized to `20.0` (°C)

#### Scenario: Imperial wind speed is normalized to m/s
- **WHEN** the unit system is `Imperial` and a wind speed value of `44.74` (mph) enters enrichment
- **THEN** the value is normalized to approximately `20.0` (m/s)

#### Scenario: Imperial pressure is normalized to hPa
- **WHEN** the unit system is `Imperial` and a pressure value of `29.92` (inHg) enters enrichment
- **THEN** the value is normalized to approximately `1013.25` (hPa)
