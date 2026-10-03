## ADDED Requirements

### Requirement: WeightedTarget is an abstract base record
`WeightedTarget` SHALL be an abstract record with properties `EndpointType EndpointType`, `LocationOptions Location`, `CycleId CycleId`, and an abstract `decimal Weight` property. Concrete subclasses SHALL override `Weight` with endpoint-specific calculation logic.

#### Scenario: Base provides common properties for budget throttling
- **WHEN** the `BudgetThrottleStage` processes a `WeightedTarget`
- **THEN** it SHALL access `Weight`, `Location`, and `CycleId` without knowing the concrete type

#### Scenario: EndpointType is available for partition routing
- **WHEN** the `Partition<WeightedTarget>` stage routes a target
- **THEN** it SHALL use `target.EndpointType.Index` to select the outlet

### Requirement: WeatherTarget carries model and weather-specific weight
`WeatherTarget` SHALL extend `WeightedTarget` with a `WeatherModel Model` property. Its `Weight` SHALL be calculated as `Math.Ceiling(hourlyVariableCount / 10.0m) * Math.Ceiling(forecastDays / 14.0m)`, matching the Open-Meteo API cost heuristic for the forecast endpoint.

#### Scenario: Weather weight calculation
- **WHEN** a `WeatherTarget` is created with 25 hourly variables and 4 forecast days
- **THEN** `Weight` SHALL be `ceil(25/10) * ceil(4/14) = 3 * 1 = 3`

#### Scenario: WeatherTarget carries the model identity
- **WHEN** the Weather sub-graph casts a `WeightedTarget` to `WeatherTarget`
- **THEN** it SHALL access `target.Model` to pass to `IWeatherClient.FetchAsync`

### Requirement: EndpointType is a value type with stable index
`EndpointType` SHALL be a value type (record struct or similar) with a `string Name` and `int Index`. The index SHALL be assigned at module registration time and SHALL remain stable for the lifetime of the pipeline materialization. The index is used as the `Partition<T>` outlet selector.

#### Scenario: Index is deterministic for registered modules
- **WHEN** modules are registered in order [Weather, AirQuality]
- **THEN** `Weather.EndpointType.Index` SHALL be 0 and `AirQuality.EndpointType.Index` SHALL be 1

#### Scenario: EndpointType equality is by name
- **WHEN** two `EndpointType` values with `Name = "weather"` are compared
- **THEN** they SHALL be equal regardless of index
