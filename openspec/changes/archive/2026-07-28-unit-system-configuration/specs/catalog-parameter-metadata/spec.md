## ADDED Requirements

### Requirement: ParameterMeta proto message
`common.proto` SHALL define a `ParameterMeta` message with `string name = 1` (the parameter's API name, e.g. `"temperature_2m"`) and `string unit = 2` (the active unit, e.g. `"°C"` or `"°F"`).

#### Scenario: Message compiles
- **WHEN** `dotnet build` runs
- **THEN** `ParameterMeta` is available in the `Njord.Grpc.V2` namespace

### Requirement: GetCatalogResponse includes parameter metadata
`GetCatalogResponse` in `weather.proto` SHALL gain a `repeated ParameterMeta parameters = 3` field. The server SHALL populate it with one entry per parameter in the active resolved parameter set, with the unit reflecting the configured unit system.

#### Scenario: Metric catalog lists metric units
- **WHEN** a client calls `GetCatalog` with unit system `Metric` and the Weather group active
- **THEN** the response contains `ParameterMeta` entries including `{name: "temperature_2m", unit: "°C"}`, `{name: "wind_speed_10m", unit: "m/s"}`, `{name: "pressure_msl", unit: "hPa"}`

#### Scenario: Imperial catalog lists imperial units
- **WHEN** a client calls `GetCatalog` with unit system `Imperial` and the Weather group active
- **THEN** the response contains `ParameterMeta` entries including `{name: "temperature_2m", unit: "°F"}`, `{name: "wind_speed_10m", unit: "mph"}`, `{name: "pressure_msl", unit: "inHg"}`

#### Scenario: Only active parameters are listed
- **WHEN** the parameter configuration is `Groups: ["Weather"], Exclude: ["cloud_cover"]`
- **THEN** the `parameters` list does NOT contain an entry with `name: "cloud_cover"`

#### Scenario: Both hourly and daily parameters are included
- **WHEN** the Weather group is active
- **THEN** the `parameters` list contains both hourly entries (e.g. `temperature_2m`) and daily entries (e.g. `temperature_2m_max`)
