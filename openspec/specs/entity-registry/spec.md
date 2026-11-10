# entity-registry Specification

## Purpose
Unified DI service for entity registration, ID lookup, and manifest building. Replaces ManifestBuilder + EntityIdLookup with name-based resolution.

## Requirements

### Requirement: Single DI service for entity registration and lookup
The system SHALL provide an `EntityRegistry` registered as a singleton DI service that
holds the complete set of entity registrations derived from enabled feature rules, configured
locations, and active models. It SHALL replace both `ManifestBuilder` and `EntityIdLookup`.

#### Scenario: Registry built from enabled features and locations
- **WHEN** the host starts with 2 locations and 30 enabled features
- **THEN** the EntityRegistry SHALL contain an entry for each (location, feature) combination
  plus model-specific weather entities

#### Scenario: Registry is injectable
- **WHEN** a service depends on EntityRegistry
- **THEN** the DI container SHALL resolve a singleton instance

### Requirement: Name-based entity ID lookup without reflection
The EntityRegistry SHALL resolve a `FeatureResult` to its entity ID using the feature's
`FeatureDescriptor.Name` and the result's `Location.Name`. It SHALL NOT use CLR type
reflection, `Assembly.GetTypes()`, or name-suffix matching. Observation results SHALL be
resolved by their `FeatureName` property.

#### Scenario: Alert result resolves to entity ID
- **WHEN** a FrostAlert result arrives for location "Vienna"
- **THEN** `Find(result)` SHALL return `"vienna_frost"`

#### Scenario: Observation result resolves by feature name
- **WHEN** a PM2.5 observation result with FeatureName "Pm25Observation" arrives for "Berlin"
- **THEN** `Find(result)` SHALL return `"berlin_pm25_observation"`

#### Scenario: Unknown result returns null
- **WHEN** a result arrives for a disabled feature
- **THEN** `Find(result)` SHALL return null

### Requirement: Manifest building as a pure mapping
The EntityRegistry SHALL expose its registrations as a list. Building
`Proto.EntityDefinition` messages from registrations SHALL be a pure mapping extension
with no state, called at the gRPC boundary.

#### Scenario: GetManifest uses registry data
- **WHEN** `GetManifest` is called
- **THEN** the gRPC service SHALL map EntityRegistry entries to Proto.EntityDefinition
  and return them
