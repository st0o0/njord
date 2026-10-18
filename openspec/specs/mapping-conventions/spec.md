# mapping-conventions Specification

## Purpose
TBD - created by archiving change consistent-mappings. Update Purpose after archive.
## Requirements
### Requirement: Mappings are small static extension classes per domain
Every mapping between layers (domain to proto, request to command, options to message or payload) SHALL be a public extension method in an `internal static` class named `<Domain><Target>Mappings`, one class per domain. A mapping class SHALL hold no state, no options and no registry lookups; private helpers are allowed only for the same domain. Mapping code SHALL NOT live inside actors or services.

#### Scenario: Analysis proto mappings are split per domain
- **WHEN** the gRPC analysis mappings are inspected
- **THEN** alerts, indices, trends, derived values and consensus each have their own mapping class and an enrichment-event dispatcher only composes them

#### Scenario: Config manager has no private mapping helpers
- **WHEN** `ConfigManager` is inspected
- **THEN** it calls `ConfigUpdateMappings` extensions for options-to-message and analysis-update construction and declares no private static mapping methods

### Requirement: Mapping names follow the direction
Domain-to-proto mappings SHALL be named `ToProto`, request-to-command mappings `ToCommand`, proto-to-domain mappings `ToDomain`, and domain-to-message mappings `ToMessage`. Mapping methods SHALL NOT be named `Map*`.

#### Scenario: Timestamp conversion
- **WHEN** a `DateTimeOffset` is converted to a protobuf timestamp
- **THEN** the single `ToTimestamp()` extension is used and no private or inline conversion exists

### Requirement: Enum-to-enum mappings live next to the other wire enums
`AlertType` and `AlertSeverity` conversions to proto SHALL be `ToProto()` extensions in `WireEnumMappings` implemented as exhaustive switch expressions whose default throws `ArgumentOutOfRangeException`.

#### Scenario: Every alert type and severity
- **WHEN** `ToProto()` is called for each `AlertType` and `AlertSeverity` member
- **THEN** each returns its documented proto member and an undefined value throws `ArgumentOutOfRangeException`

### Requirement: Option-derived values are produced by one extension
`ParametersOptions.Resolve()` SHALL produce the `ResolvedParameterSet` and be the only call site of `ParameterRegistry.Resolve` for options. `PollingOptions.ToInitialBudgetSnapshot()` SHALL produce the starting `BudgetHealthSnapshot`.

#### Scenario: Validator and container agree
- **WHEN** the options validator and the container resolve the same `ParametersOptions`
- **THEN** both use `Resolve()` and obtain the same set

### Requirement: Each mapping class has a spec and the rules are enforced
Every mapping class SHALL have a spec that pins every field of every mapping. `MappingConventionArchitectureSpec` SHALL fail when a `*Mappings` class is not `internal static`, contains instance members or state, or when a production type outside such a class declares a private static method named `Map*` that builds a contract or proto type.

#### Scenario: A new mapper breaks the convention
- **WHEN** a `*Mappings` class is added with a non-static member
- **THEN** the architecture spec fails

#### Scenario: Behaviour is unchanged
- **WHEN** the gRPC and MQTT golden masters are verified after the refactor
- **THEN** they are byte-identical to before

