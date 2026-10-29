# array-default-pattern Specification

## Purpose
Config classes with array properties that have non-empty defaults declare them through `IHasArrayDefaults`, applied automatically after binding by `AddNjordOptions` and `AddFeature`, so that no registration code carries inline array-default logic.

## Requirements
### Requirement: Array defaults declared via IHasArrayDefaults
Config classes with array properties that have non-empty defaults SHALL implement `IHasArrayDefaults` (instance method `ApplyDefaults()`). The array property initializer SHALL be `[]`; the built-in defaults SHALL be exposed as a separate `static` property. `ApplyDefaults()` SHALL fill the empty array with the defaults only when the array has zero elements after binding.

#### Scenario: Config with unset thresholds gets defaults
- **WHEN** a config section exists but does not set `Thresholds`
- **THEN** `ApplyDefaults()` SHALL fill `Thresholds` with the static default array

#### Scenario: User-provided thresholds are preserved
- **WHEN** a config section sets `Thresholds` to `[5, 10, 20]`
- **THEN** `ApplyDefaults()` SHALL not overwrite the user's values

#### Scenario: Config class with multiple array properties
- **WHEN** a config declares both `CapeThresholds` and other arrays
- **THEN** `ApplyDefaults()` SHALL fill each empty array independently

### Requirement: AddNjordOptions auto-registers PostConfigure for IHasArrayDefaults
`AddNjordOptions<T>()` SHALL detect when `T` implements `IHasArrayDefaults` and automatically register a `PostConfigure<T>` that calls `ApplyDefaults()`. No manual PostConfigure registration SHALL be needed for array defaults.

#### Scenario: Registration without explicit PostConfigure
- **WHEN** `AddNjordOptions<FrostConfig>()` is called and `FrostConfig` implements `IHasArrayDefaults`
- **THEN** a PostConfigure SHALL be registered that invokes `ApplyDefaults()` before validation

#### Scenario: Non-array config is unaffected
- **WHEN** `AddNjordOptions<FogConfig>()` is called and `FogConfig` does not implement `IHasArrayDefaults`
- **THEN** no PostConfigure SHALL be registered

### Requirement: AddFeature combines options and rule registration
`AddFeature<TRule, TConfig>()` SHALL register the config via `AddNjordOptions<TConfig>()` and the rule via `AddSingleton<IFeatureRule, TRule>()` in a single call. A three-type-parameter overload `AddFeature<TRule, TConfig, TValidator>()` SHALL additionally register the validator.

#### Scenario: Feature with validator registered in one call
- **WHEN** `AddFeature<FrostAlert, FrostConfig, FrostConfigValidator>()` is called
- **THEN** `FrostConfig` SHALL be bound, its validator registered, PostConfigure auto-applied for `IHasArrayDefaults`, and `FrostAlert` registered as `IFeatureRule`

#### Scenario: Feature without validator registered in one call
- **WHEN** `AddFeature<FogAlert, FogConfig>()` is called
- **THEN** `FogConfig` SHALL be bound with no validator and `FogAlert` registered as `IFeatureRule`

### Requirement: No manual PostConfigure for array defaults in AddFeatures
The `AddFeatures` registration method SHALL NOT contain any inline PostConfigure lambdas for filling empty arrays with defaults. All array-default logic SHALL be encapsulated in the config class's `ApplyDefaults()` implementation.

#### Scenario: AddFeatures contains only AddFeature calls and AddSingleton calls
- **WHEN** `ServiceCollectionExtensions.AddFeatures` is inspected
- **THEN** it SHALL contain no `PostConfigure` calls for feature config array defaults
