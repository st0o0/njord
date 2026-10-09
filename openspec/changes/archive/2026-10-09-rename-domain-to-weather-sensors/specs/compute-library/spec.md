## MODIFIED Requirements

### Requirement: Njord.Compute is a pure computation library
The solution SHALL contain a `Njord.Compute` class library that holds all weather analysis computation types (consensus, alerts, derived values, trends, indices, history, time-slice aggregation, preference resolution). The library SHALL reference only `Njord.Weather` and `Njord.Sensors` — no Akka, no DI, no ASP.NET dependencies.

#### Scenario: Compute references only Weather and Sensors
- **WHEN** `Njord.Compute.csproj` is inspected
- **THEN** its only `ProjectReference`s SHALL be `Njord.Weather` and `Njord.Sensors`

#### Scenario: Compute contains no Akka types
- **WHEN** `Njord.Compute` is built
- **THEN** it SHALL NOT reference any `Akka.*` package

#### Scenario: Analysis namespace preserved
- **WHEN** types are moved from `Njord.Core/Analysis/` to `Njord.Compute`
- **THEN** they SHALL retain the `Njord.Analysis` namespace
