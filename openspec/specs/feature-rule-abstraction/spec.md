# feature-rule-abstraction Specification

## Purpose
Typed FeatureRule abstraction: features are identified by their C# type, read typed input records and emit typed results.

## Requirements

### Requirement: FeatureRule base with typed input
The system SHALL provide an abstract `FeatureRule<TInput>` base class that every feature implements. `TInput` is a readonly record struct containing only the domain forecasts the feature declared in its requirements. The base class SHALL expose two abstract methods: `Project` (extracts `TInput` from `LocationForecast`, returning null when required domains are missing) and `Evaluate` (pure evaluation taking `FeatureContext<TInput>`, returning a `FeatureResult` or null to skip).

#### Scenario: Feature receives only declared domains
- **WHEN** a feature declares `Requirements: Weather(Temperature2m)` and uses `WeatherInput`
- **THEN** its `Evaluate` method receives only `WeatherInput` containing the weather forecast, and has no compile-time access to AirQuality, Flood, Marine, Seasonal, Satellite or Ensemble data

#### Scenario: Feature skips when domains are missing
- **WHEN** `Project` is called with a `LocationForecast` that lacks a required domain
- **THEN** `Project` SHALL return null and the feature SHALL not be evaluated

#### Scenario: Feature accesses ensemble spread for confidence
- **WHEN** an alert declares `Requirements: Weather(Temperature), Ensemble(TemperatureSpread)` and its `Project` receives a `LocationForecast` with both Weather and Ensemble data
- **THEN** the alert's input SHALL include the ensemble spread data for confidence lookup

#### Scenario: Alert runs without ensemble when ensemble is not required
- **WHEN** an alert declares only `Requirements: Weather(Pressure)` without Ensemble
- **THEN** `Project` SHALL succeed with Weather data alone, and the alert SHALL use Medium confidence

### Requirement: Typed input records for domain combinations
The system SHALL provide shared readonly record struct input types for common domain combinations. At minimum: `WeatherInput`, `WeatherEnsembleInput`, `WeatherAqInput`, `WeatherSolarInput`, `FloodWeatherInput`, `FloodMarineWeatherInput`, `MarineWeatherInput`, `SeasonalInput`. A feature that needs a combination not covered SHALL define its own input record.

#### Scenario: Single-domain feature uses single-domain input
- **WHEN** a weather-only feature is defined (e.g. PressureDrop which does not need confidence)
- **THEN** it SHALL use `WeatherInput` containing only `DomainForecast<WeatherPoint>`

#### Scenario: Weather + Ensemble feature uses combined input
- **WHEN** an alert requires both weather data and ensemble spread for confidence
- **THEN** it SHALL use `WeatherEnsembleInput` containing `DomainForecast<WeatherPoint>` and `DomainForecast<EnsemblePoint>`

#### Scenario: Cross-domain feature uses multi-domain input
- **WHEN** a feature requires Weather, Marine and Flood data
- **THEN** it SHALL use `FloodMarineWeatherInput` containing all three domain forecasts

### Requirement: Feature has no string identifier
A feature SHALL NOT have a `Name` property, a string identifier, or any mechanism to retrieve its textual identity at runtime. The C# type IS the identity. Wire keys and config section paths are derived externally, never stored on or retrievable from the feature.

#### Scenario: Feature type used as sole identity
- **WHEN** the system needs to distinguish features
- **THEN** it SHALL compare by C# type (`typeof(FrostAlert)`), never by a string property

### Requirement: FeatureDescriptor without Name
`FeatureDescriptor` SHALL contain `OutputKind Output`, `FeatureCategory Category`, `DomainRequirements Requirements`, and `int PollDepth`. It SHALL NOT contain a `Name`, any string identifier, or a `DefaultEnabled` flag. The default enabled state is determined solely by the config class property initializer.

#### Scenario: Descriptor carries all metadata except identity and defaults
- **WHEN** a feature exposes its descriptor
- **THEN** the descriptor SHALL contain category, output kind, domain requirements and poll depth, but no name, wire key, or default-enabled flag

### Requirement: Typed feature result hierarchy
Each feature SHALL return a feature-specific sealed record that extends an abstract base per `OutputKind`. The base records SHALL be `AlertFeatureResult`, `ScoreFeatureResult`, `ConditionFeatureResult`, `TrendFeatureResult`, `ForecastFeatureResult`. Feature-specific records (e.g. `FrostAlertResult`) SHALL carry feature-specific data fields alongside the base fields.

#### Scenario: Alert feature returns typed result with feature data
- **WHEN** `FrostAlert.Evaluate` produces a result
- **THEN** it SHALL return a `FrostAlertResult` containing `Severity`, `StartsAt`, `EndsAt`, `MinTemperature`, `DewPoint`

#### Scenario: Score feature returns typed result with score data
- **WHEN** `BbqScore.Evaluate` produces a result
- **THEN** it SHALL return a `BbqScoreResult` containing `Score`, `BestStart`, `BestHours`

### Requirement: FeatureContext includes poll history
`FeatureContext<TInput>` SHALL provide `Current` (the latest projected input), `History` (a list of projected inputs from past polls, most recent first, length = `PollDepth + 1`), and `Now` (`DateTimeOffset`). When fewer polls than `PollDepth` have occurred since startup, `History` SHALL contain only the available entries.

#### Scenario: Feature with PollDepth 0 receives current only
- **WHEN** a feature declares `PollDepth = 0`
- **THEN** `FeatureContext.History` SHALL have exactly one entry (the current input)

#### Scenario: Feature with PollDepth 8 receives up to 9 entries
- **WHEN** a feature declares `PollDepth = 8` and 20 polls have occurred
- **THEN** `FeatureContext.History` SHALL have exactly 9 entries (current + 8 previous)

#### Scenario: Insufficient polls
- **WHEN** a feature declares `PollDepth = 8` but only 3 polls have occurred
- **THEN** `FeatureContext.History` SHALL have 3 entries, not 9

### Requirement: Alert confidence from ensemble spread
Alert features that require ensemble data SHALL access confidence through the ensemble forecast in their typed input, not through a field on WeatherPoint. Confidence SHALL be derived by looking up the `EnsemblePoint` at the relevant timestamp and passing its spread value to `ConfidenceThresholds.LevelFor`. When ensemble data is absent from the input (because the alert does not require it), confidence SHALL be `ConfidenceLevel.Medium`.

#### Scenario: Alert with ensemble calculates confidence from spread
- **WHEN** FrostAlert evaluates with ensemble data available
- **THEN** it SHALL look up the EnsemblePoint at the coldest WeatherPoint's ValidFrom and pass `TemperatureSpread` to `ConfidenceThresholds.LevelFor`

#### Scenario: Alert without ensemble uses Medium confidence
- **WHEN** PressureDropAlert evaluates (it does not require Ensemble)
- **THEN** confidence SHALL be `ConfidenceLevel.Medium`

#### Scenario: Ensemble data not yet polled
- **WHEN** FrostAlert evaluates but the Ensemble domain has not completed its first poll
- **THEN** `Project` SHALL return null (required domain missing) and the alert SHALL not fire until both Weather and Ensemble data are available
