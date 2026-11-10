## MODIFIED Requirements

### Requirement: Name-based entity ID lookup without reflection
The EntityRegistry SHALL resolve a `FeatureResult` to its entity ID using the feature's `FeatureDescriptor.Name` and the result's `Location.Name`. It SHALL NOT use CLR type reflection, `Assembly.GetTypes()`, or name-suffix matching. Observation results SHALL be resolved by their `FeatureName` property. WeatherResults with a non-null ModelId SHALL be resolved to their per-model entity ID.

#### Scenario: Alert result resolves to entity ID
- **WHEN** a FrostAlert result arrives for location "Vienna"
- **THEN** `Find(result)` SHALL return `"vienna_frost"`

#### Scenario: Primary weather result resolves to entity ID
- **WHEN** a WeatherResult with ModelId = null arrives for location "Barcelona"
- **THEN** `Find(result)` SHALL return `"barcelona_weather"`

#### Scenario: Per-model weather result resolves to entity ID
- **WHEN** a WeatherResult with ModelId = "ecmwf_ifs025_ensemble_mean" arrives for location "Barcelona"
- **THEN** `Find(result)` SHALL return `"barcelona_ecmwf_ifs025_ensemble_mean_weather"`

#### Scenario: Unknown model ID returns null
- **WHEN** a WeatherResult with ModelId = "nonexistent_model" arrives
- **THEN** `Find(result)` SHALL return null (entity not registered)
