## ADDED Requirements

### Requirement: Handler projects are organized by output pattern

The solution SHALL contain exactly four handler projects grouped by `OutputKind` pattern: `Njord.Alerts`, `Njord.Scores`, `Njord.Insights`, `Njord.Advisories`. Each SHALL reference only `Njord.Core` (and its transitive dependencies), never another handler project, never the Host.

#### Scenario: Alerts project contains all IAlertRule-based handlers

- **WHEN** the `Njord.Alerts` project is inspected
- **THEN** it SHALL contain `WeatherAlertHandler`, `AirQualityAlertHandler`, `FloodRiskHandler`, `FloodRainHandler`, `SwimmingConditionsHandler`, all alert rules (14 weather, 2 air quality, 2 flood, 1 marine), `AlertWindow`, `AirQualityAlertWindow`, and `FloodRainInput`

#### Scenario: Scores project contains all window-score and score-condition handlers

- **WHEN** the `Njord.Scores` project is inspected
- **THEN** it SHALL contain `BBQScoreHandler`, `LaundryScoreHandler`, `OutdoorScoreHandler`, `SurfConditionsHandler`, `SurfConditionsRule`, and `Scoring`

#### Scenario: Insights project contains passthrough and trend handlers

- **WHEN** the `Njord.Insights` project is inspected
- **THEN** it SHALL contain `SeasonalOutlookHandler`, `SolarYieldHandler`, `SolarIrradianceBlend`, `SatelliteRadiationHandler`, `ConfidenceTrendHandler`, `ApproachingFrontHandler`, and `PollenTrendHandler`

#### Scenario: Advisories project contains condition-check handlers

- **WHEN** the `Njord.Advisories` project is inspected
- **THEN** it SHALL contain `CommuteSafetyHandler`, `VentilationWindowHandler`, `NightVentilationHandler`, `ExerciseAdvisoryHandler`, `GardeningHandler`, and `FloodTimelineHandler`

#### Scenario: No handler remains in a deleted project

- **WHEN** the solution is built
- **THEN** `Njord.Weather`, `Njord.AirQuality`, `Njord.Flood`, and `Njord.Marine` SHALL NOT exist as projects

### Requirement: Each handler project has a matching test project

Each handler project SHALL have a corresponding test project (`Njord.Alerts.Tests`, `Njord.Scores.Tests`, `Njord.Insights.Tests`, `Njord.Advisories.Tests`). All existing tests SHALL move to the matching new test project with no test loss.

#### Scenario: Test count is preserved

- **WHEN** all test projects are run after the reorganization
- **THEN** the total number of passing tests across `Njord.Alerts.Tests`, `Njord.Scores.Tests`, `Njord.Insights.Tests`, and `Njord.Advisories.Tests` SHALL equal the combined total from the former `Njord.Weather.Tests`, `Njord.AirQuality.Tests`, `Njord.Flood.Tests`, and `Njord.Marine.Tests`

### Requirement: Registration stays in the Host

Each handler project SHALL expose a single `IServiceCollection` extension method for DI registration. The Host SHALL call all four methods. The method names SHALL be `AddAlertHandlers`, `AddScoreHandlers`, `AddInsightHandlers`, `AddAdvisoryHandlers`.

#### Scenario: Host registers all handler projects

- **WHEN** the Host starts
- **THEN** it SHALL call `AddAlertHandlers()`, `AddScoreHandlers()`, `AddInsightHandlers()`, and `AddAdvisoryHandlers()` to register all handlers and rules

### Requirement: Architecture zone enforcement covers the new projects

`ArchitectureSpec` SHALL enforce the same guardrails on the new projects as on the old ones: handler projects reference only `Njord.Core` and below, never each other, never the Host. All handler classes SHALL remain sealed.

#### Scenario: Cross-reference between handler projects is rejected

- **WHEN** `Njord.Alerts` adds a reference to `Njord.Scores`
- **THEN** `ArchitectureSpec` SHALL fail

#### Scenario: Handler project referencing Host is rejected

- **WHEN** `Njord.Advisories` adds a reference to `Njord`
- **THEN** `ArchitectureSpec` SHALL fail
