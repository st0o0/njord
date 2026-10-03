## ADDED Requirements

### Requirement: Production and test code SHALL NOT contain XML doc comments

Per the project convention "no XML docs — code speaks through naming", all
`/// <summary>` and related XML doc comments SHALL be removed from production
and test code.

#### Scenario: Options class has no XML docs
- **WHEN** inspecting `NjordOptions.cs` or `MqttOptions.cs`
- **THEN** no `///` XML doc comments exist on the class or its properties

#### Scenario: Domain records have no XML docs
- **WHEN** inspecting domain records like `CycleId`, `WeatherModel`, `ForecastSeries`, `FetchOutcome`
- **THEN** no `///` XML doc comments exist

#### Scenario: Test helpers have no XML docs
- **WHEN** inspecting test helper classes like `FailingRefProvider`
- **THEN** no `///` XML doc comments exist
