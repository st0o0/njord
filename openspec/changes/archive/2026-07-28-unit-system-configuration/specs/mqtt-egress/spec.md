## MODIFIED Requirements

### Requirement: Discovery component metadata adapts to registry
Each sensor component in the discovery payload SHALL derive its `unit_of_measurement` from `UnitResolver.GetUnit(param, unitSystem)` using the configured unit system, its `device_class` from the parameter registry entry, and its `name` from the registry. Parameters with `device_class: null` SHALL omit the `device_class` field. Parameters with value type `TimeString` SHALL use `device_class: "timestamp"` when appropriate or omit it.

#### Scenario: Metric unit in discovery
- **WHEN** unit system is `Metric` and `shortwave_radiation` (unit `W/m²`, device_class `irradiance`) is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "W/m²"` and `"device_class": "irradiance"`

#### Scenario: Imperial unit in discovery for temperature
- **WHEN** unit system is `Imperial` and `temperature_2m` is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "°F"` and `"device_class": "temperature"`

#### Scenario: Imperial unit in discovery for wind speed
- **WHEN** unit system is `Imperial` and `wind_speed_10m` is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "mph"` and `"device_class": "wind_speed"`

#### Scenario: Imperial unit in discovery for pressure
- **WHEN** unit system is `Imperial` and `pressure_msl` is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "inHg"` and `"device_class": "atmospheric_pressure"`

#### Scenario: Parameter without device class
- **WHEN** `weather_code` (no device_class) is in the active set
- **THEN** its discovery component carries `"unit_of_measurement": "wmo code"` and no `device_class` field

### Requirement: DiscoveryPayloadBuilder builds a derived device
`DiscoveryPayloadBuilder.BuildDerived` SHALL produce a device-based discovery payload for location with device id `njord_{location}_derived`, model `derived`, and sensor components for: each horizon-based derived value (beaufort, wind_chill, dewpoint_comfort, wmo_description) at each configured horizon, plus scalar sensors (diurnal_amplitude, sunshine_pct, inversion). Numeric sensors SHALL have `unit_of_measurement` derived from `UnitResolver` where applicable (wind_chill uses the active temperature unit, diurnal_amplitude uses the active temperature unit, decay_rate uses the active temperature unit per hour). String sensors (dewpoint_comfort, wmo_description) SHALL use platform `sensor` with no unit. The boolean sensor (inversion) SHALL use platform `binary_sensor`.

#### Scenario: Derived device payload structure
- **WHEN** `BuildDerived` is called for location "lucerne" with horizons [3, 6, 12, 24, 48, 72]
- **THEN** the payload contains device id "njord_lucerne_derived", model "derived", and sensor components

#### Scenario: Wind chill uses active temperature unit
- **WHEN** unit system is `Imperial` and the derived device is built with horizon 3
- **THEN** the wind_chill sensor component has `"unit_of_measurement": "°F"`

#### Scenario: Diurnal amplitude uses active temperature unit
- **WHEN** unit system is `Imperial` and the derived device is built
- **THEN** diurnal_amplitude has `"unit_of_measurement": "°F"`
