## ADDED Requirements

### Requirement: Njord.Model project contains vocabulary types
The system SHALL have a project `Njord.Model` (namespace `Njord.Model`) with zero project dependencies, containing the domain vocabulary types: `ParameterDef`, `ParameterRegistry`, `ParameterGroup`, `ParameterGranularity`, `ParameterValueType`, `WeatherModel`, `LocationId`, `Horizon`, `HorizonKind`, `CycleId`, `WeatherCondition`, `WeatherConditions`, `ResolvedParameterSet`, and `ForecastValue`.

#### Scenario: Model has no project references
- **WHEN** inspecting `Njord.Model.csproj`
- **THEN** it SHALL contain zero `<ProjectReference>` elements

#### Scenario: Vocabulary types are accessible from all tiers
- **WHEN** any project in the solution references `Njord.Model`
- **THEN** it SHALL have access to all vocabulary types without pulling in Akka, Forecast, or any other domain dependency

### Requirement: Njord.Forecast project contains time-series types
The system SHALL have a project `Njord.Forecast` (namespace `Njord.Forecast`) referencing only `Njord.Model`, containing: `Forecast`, `ForecastSeries`, `ForecastPoint`, `DailyForecastSeries`, `DailyForecastPoint`, `ModelForecasts`, `HorizonProjection`, `TimeAnchor`, `ForecastDataHash`, `PollOutcome`, `PollFailureReason`, and `HorizonValue`.

#### Scenario: Forecast references only Model
- **WHEN** inspecting `Njord.Forecast.csproj`
- **THEN** it SHALL contain exactly one `<ProjectReference>` to `Njord.Model`

#### Scenario: Analysis features cannot reference Forecast
- **WHEN** `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, or `Njord.Indices` attempts to add a reference to `Njord.Forecast`
- **THEN** `LayerReferenceSpec` SHALL fail, enforcing that analysis features work on `LocationConsensus` only

### Requirement: Njord.Weather project no longer exists
The `Njord.Weather` project and namespace SHALL be removed. All types formerly in `Njord.Weather` SHALL live in either `Njord.Model` or `Njord.Forecast`.

#### Scenario: No residual Weather references
- **WHEN** building the solution after the restructure
- **THEN** no production or test file SHALL contain `using Njord.Weather` or `<ProjectReference>` to `Njord.Weather`

### Requirement: LocationOptions relocated to Core
`LocationOptions` SHALL move from `Njord.Weather` to `Njord.Core/Configuration/` (namespace `Njord.Core.Configuration`), because it is a configuration DTO, not a domain type.

#### Scenario: LocationOptions in Core namespace
- **WHEN** code references `LocationOptions`
- **THEN** the using directive SHALL be `Njord.Core.Configuration`, not `Njord.Model` or `Njord.Forecast`

### Requirement: Updated project references
The following projects SHALL update their references to reflect the split:
- `Njord.Consensus` → `Njord.Model` + `Njord.Forecast`
- `Njord.Messages` → `Njord.Model` + `Njord.Sensors` + `Njord.Consensus` + `Njord.Forecast`
- `Njord.Core` → `Njord.Model` + `Njord.Forecast` + `Njord.Messages` + `Njord.Persistence` + `Njord.Consensus`

#### Scenario: Feature libraries reference only Core
- **WHEN** inspecting `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`, `Njord.OpenMeteo`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Mqtt`, `Njord.Grpc`, or `Njord.Pipeline`
- **THEN** each SHALL reference `Njord.Core` (and Pipeline additionally references the analysis libraries) — none SHALL have a direct reference to `Njord.Model` or `Njord.Forecast` (they receive them transitively through Core)

### Requirement: Namespace rename is complete
Every `using Njord.Weather` in production and test code SHALL be replaced with `using Njord.Model` or `using Njord.Forecast` depending on which types the file uses.

#### Scenario: Grep finds no Weather namespace
- **WHEN** running `grep -r "namespace Njord.Weather" src/` or `grep -r "using Njord.Weather" src/`
- **THEN** the result SHALL be empty (excluding obj/ directories)
