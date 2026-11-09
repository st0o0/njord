## MODIFIED Requirements

### Requirement: FeatureDescriptor without Name
`FeatureDescriptor` SHALL contain `OutputKind Output`, `FeatureCategory Category`, `string Name`, `DomainRequirements Requirements`, `int PollDepth`, and `EntityMetadata EntityMetadata`. The `EntityMetadata` property SHALL provide the entity platform, device class, icon, and unit that the manifest uses to declare the entity to clients.

#### Scenario: Descriptor carries all metadata except identity and defaults
- **WHEN** a feature exposes its descriptor
- **THEN** the descriptor SHALL contain category, output kind, name, domain requirements, poll depth, and entity metadata

#### Scenario: EntityMetadata is present on every descriptor
- **WHEN** any enabled feature rule's descriptor is accessed
- **THEN** `EntityMetadata` SHALL NOT be null and SHALL have at minimum a `Platform` value

### Requirement: Typed feature result hierarchy
Each feature SHALL return a feature-specific sealed record that extends an abstract base per `OutputKind`. The base records SHALL be `AlertFeatureResult`, `ScoreFeatureResult`, `ConditionFeatureResult`, `TrendFeatureResult`, `ForecastFeatureResult`, `ObservationFeatureResult`, `WeatherFeatureResult`. Feature-specific records (e.g. `FrostAlertResult`) SHALL carry feature-specific data fields alongside the base fields.

#### Scenario: Alert feature returns typed result with feature data
- **WHEN** `FrostAlert.Evaluate` produces a result
- **THEN** it SHALL return a `FrostAlertResult` containing `Severity`, `StartsAt`, `EndsAt`, `MinTemperature`, `DewPoint`

#### Scenario: Score feature returns typed result with score data
- **WHEN** `BbqScore.Evaluate` produces a result
- **THEN** it SHALL return a `BbqScoreResult` containing `Score`, `BestStart`, `BestHours`

#### Scenario: Observation feature returns ObservationFeatureResult
- **WHEN** a PM2.5 observation feature evaluates
- **THEN** it SHALL return an `ObservationFeatureResult`-derived record containing the numeric value

#### Scenario: Weather feature returns WeatherFeatureResult
- **WHEN** the WeatherRule evaluates with forecast data
- **THEN** it SHALL return a `WeatherFeatureResult`-derived record containing current conditions and forecast array

## ADDED Requirements

### Requirement: EntityMetadata record
The system SHALL provide an `EntityMetadata` record with properties: `EntityPlatform Platform`, `string? DeviceClass`, `string? Icon`, `string? Unit`. `Platform` SHALL be required; all others are optional. `EntityPlatform` SHALL be an enum with values: `Weather`, `Sensor`, `BinarySensor`.

#### Scenario: Alert feature declares BinarySensor platform
- **WHEN** a frost alert feature defines its descriptor
- **THEN** `EntityMetadata.Platform` SHALL be `BinarySensor` and `DeviceClass` SHALL be `"safety"`

#### Scenario: Score feature declares Sensor platform
- **WHEN** a commute score feature defines its descriptor
- **THEN** `EntityMetadata.Platform` SHALL be `Sensor` and `Unit` SHALL be `"%"`

### Requirement: Two new OutputKind values
`OutputKind` SHALL include `Weather` and `Observation` in addition to the existing `Alert`, `Score`, `Condition`, `Trend`, `Forecast`.

#### Scenario: OutputKind.Weather exists
- **WHEN** the OutputKind enum is inspected
- **THEN** it SHALL contain a `Weather` member

#### Scenario: OutputKind.Observation exists
- **WHEN** the OutputKind enum is inspected
- **THEN** it SHALL contain an `Observation` member
