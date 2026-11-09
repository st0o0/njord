## MODIFIED Requirements

### Requirement: ObservationResult carries a numeric value
`ObservationResult` SHALL be a result type containing a `string FeatureName` and a `double Value`. The `FeatureName` SHALL identify which observation feature produced the result, matching the feature's `FeatureDescriptor.Name`. It SHALL extend the feature result hierarchy as the result base for `OutputKind.Observation`.

#### Scenario: ObservationResult is a valid feature result
- **WHEN** an observation feature named "Pm25" returns an `ObservationResult("Pm25", 42.5)`
- **THEN** the pipeline SHALL process it like any other feature result and route it to the entity stream

#### Scenario: ObservationResult carries the producing feature's name
- **WHEN** the PM2.5 observation feature produces a result
- **THEN** the `FeatureName` SHALL equal the feature's descriptor name ("Pm25")

### Requirement: Observation OutputKind for raw domain values
The system SHALL support `OutputKind.Observation` for features that pass through a single raw domain value as a sensor entity. An observation feature SHALL be a trivial `FeatureRule` whose `Evaluate` method extracts one numeric value from the domain data and returns an `ObservationResult` with its descriptor name as the `FeatureName`.

#### Scenario: PM2.5 observation feature
- **WHEN** the PM2.5 observation feature is enabled and AirQuality data contains a PM2.5 value
- **THEN** the feature SHALL produce an `ObservationResult` with that numeric value and `FeatureName` "Pm25"

#### Scenario: Observation feature skips when value is absent
- **WHEN** the domain data does not contain the observed field (null or missing)
- **THEN** the feature SHALL return null (skip)
