# post-configure-defaults Specification

## Purpose

Replaces the nullable+Effective* options pattern with PostConfigure<T> that fills null or empty arrays with defaults at startup, so IOptions<T>.Value always carries the resolved value.

## Requirements

### Requirement: Null arrays are filled with defaults at startup
Options classes that carry nullable arrays with fallback defaults (ForecastOptions.Models, PollingOptions.BudgetOverride, and the feature config Thresholds) SHALL use PostConfigure<T> to replace null or empty with the default value. The Effective* computed properties SHALL NOT exist.

#### Scenario: ForecastOptions.Models is non-nullable after startup
- **WHEN** no `Njord:Forecast:Models` is configured
- **THEN** `IOptions<ForecastOptions>.Value.Models` returns the default array containing the default model id
- **THEN** there is no `EffectiveModels` or `PrimaryModel` property

#### Scenario: Feature config Thresholds are non-nullable after startup
- **WHEN** no Thresholds are configured for a feature
- **THEN** `IOptions<TConfig>.Value.Thresholds` returns the feature's default threshold array
- **THEN** there is no `EffectiveThresholds` computed property

#### Scenario: Explicit configuration overrides the default
- **WHEN** `Njord:Forecast:Models` is set to a custom array
- **THEN** `IOptions<ForecastOptions>.Value.Models` returns the configured array, not the default
