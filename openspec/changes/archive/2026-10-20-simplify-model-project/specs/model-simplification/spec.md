## REMOVED Requirements

### Requirement: ModelCoverageRegistry provides per-model capability data
**Reason**: Individual model coverage data is unnecessary in the Ensemble-default architecture. The catalog no longer needs per-model parameter lists.
**Migration**: ServiceInfoMappings builds a simplified catalog from location names and enabled domains, not model coverage.

#### Scenario: Catalog without model coverage
- **WHEN** a gRPC client requests the catalog
- **THEN** the response SHALL list locations with their enabled domains, not per-model parameter capabilities

### Requirement: WeatherModel enum identifies forecast models
**Reason**: Polling is per-location with Ensemble, not per-model. WeatherModel is only used by a legacy MQTT topic path.
**Migration**: MQTT topics use domain-based paths (`njord/{loc}/{domain}/{param}`), not model-based paths.

#### Scenario: No model-based MQTT topics
- **WHEN** the MQTT publisher emits topics
- **THEN** no topic path SHALL contain a WeatherModel identifier

### Requirement: WeatherCondition enum maps WMO codes
**Reason**: WMO weather codes are returned as integers in WeatherPoint. A domain enum adds no value over the raw integer.
**Migration**: Consumers interpret the WMO code integer directly.

#### Scenario: WeatherPoint carries raw WMO code
- **WHEN** a WeatherPoint is created from API data
- **THEN** the WeatherCode field SHALL be an integer, not a WeatherCondition enum

## MODIFIED Requirements

### Requirement: Parameter types are weather-client-internal
ParameterDef, ParameterRegistry, and ResolvedParameterSet SHALL be internal types in Njord.OpenMeteo, not public types in Njord.Model. They drive the Ensemble request URL variable list and are not part of the shared domain vocabulary. AirQuality and Flood clients SHALL own their own variable lists independently.

#### Scenario: ParameterRegistry not visible from Messages
- **WHEN** Njord.Messages is compiled
- **THEN** it SHALL have no reference to ParameterDef, ParameterRegistry, or ResolvedParameterSet

#### Scenario: AQ client owns its variable list
- **WHEN** an Air Quality fetch URL is built
- **THEN** the variable names SHALL come from the AQ client code, not from ParameterRegistry

### Requirement: Njord.Model contains only domain-neutral vocabulary
After this change, Njord.Model SHALL contain only: Location, DomainKind, TimeResolution, IHasValidTime, DomainForecast<T>, IDomainForecast, and the five domain point types. No business logic, no registries, no configuration concerns.

#### Scenario: Model project has no business logic
- **WHEN** the Njord.Model assembly is inspected
- **THEN** it SHALL contain only record types, enums, and interfaces with no static registries or computation methods
