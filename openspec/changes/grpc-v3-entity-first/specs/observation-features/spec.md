## ADDED Requirements

### Requirement: Observation OutputKind for raw domain values
The system SHALL support `OutputKind.Observation` for features that pass through a single raw domain value as a sensor entity. An observation feature SHALL be a trivial `FeatureRule` whose `Evaluate` method extracts one numeric value from the domain data and returns an `ObservationResult`.

#### Scenario: PM2.5 observation feature
- **WHEN** the PM2.5 observation feature is enabled and AirQuality data contains a PM2.5 value
- **THEN** the feature SHALL produce an `ObservationResult` with that numeric value

#### Scenario: Observation feature skips when value is absent
- **WHEN** the domain data does not contain the observed field (null or missing)
- **THEN** the feature SHALL return null (skip)

### Requirement: ObservationResult carries a numeric value
`ObservationResult` SHALL be a result type containing a `double Value`. It SHALL extend the feature result hierarchy as the result base for `OutputKind.Observation`.

#### Scenario: ObservationResult is a valid feature result
- **WHEN** an observation feature returns an `ObservationResult(42.5)`
- **THEN** the pipeline SHALL process it like any other feature result and route it to the entity stream

### Requirement: Observation features declare EntityMetadata
Each observation feature's `FeatureDescriptor` SHALL include `EntityMetadata` with `Platform = Sensor` and the appropriate `DeviceClass` and `Unit` for the observed value (e.g. `DeviceClass = "pm25"`, `Unit = "ug/m3"` for PM2.5).

#### Scenario: Observation entity appears in manifest as sensor
- **WHEN** the PM2.5 observation feature is enabled
- **THEN** the manifest SHALL include an entity with `platform = sensor`, `device_class = "pm25"`, `unit = "ug/m3"`

### Requirement: Sample observation features for validation
The initial implementation SHALL include at least two observation features covering different domains to validate the pattern: one from AirQuality (e.g. PM2.5) and one from another domain (e.g. Marine wave height or Flood discharge).

#### Scenario: Multiple domains covered
- **WHEN** the observation feature set is registered
- **THEN** at least two distinct `DomainKind` values SHALL be represented among observation features
