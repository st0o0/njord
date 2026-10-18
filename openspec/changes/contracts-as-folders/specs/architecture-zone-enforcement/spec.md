## ADDED Requirements

### Requirement: Layer reference direction across consensus and messages
Foundation libraries SHALL follow this reference direction: `Njord.Weather`, `Njord.Sensors`, and `Persistence` reference no Njord assembly; `Njord.Consensus` references only `Njord.Weather` and `Njord.Sensors`; `Messages` references only `Njord.Weather`, `Njord.Sensors` and `Njord.Consensus`; `Core` references only `Njord.Weather`, `Njord.Sensors`, `Messages`, `Persistence` and `Njord.Consensus`. Feature libraries reference only `Core` and below, except `Njord.Pipeline`, which may also reference the analysis libraries (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`). The host references all. `LayerReferenceSpec` SHALL check every assembly.

#### Scenario: Consensus references only Weather and Sensors
- **WHEN** `LayerReferenceSpec` checks `Njord.Consensus` assembly references
- **THEN** the only Njord references SHALL be `Njord.Weather` and `Njord.Sensors`

#### Scenario: Messages references Consensus
- **WHEN** `LayerReferenceSpec` checks `Njord.Messages` assembly references
- **THEN** the allowed Njord references SHALL be `Njord.Weather`, `Njord.Sensors` and `Njord.Consensus`

#### Scenario: Core references include Consensus
- **WHEN** `LayerReferenceSpec` checks `Njord.Core` assembly references
- **THEN** the allowed Njord references SHALL be `Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, `Njord.Persistence` and `Njord.Consensus`

#### Scenario: Analysis libraries reference only Core and below
- **WHEN** `LayerReferenceSpec` checks an analysis library's assembly references
- **THEN** the allowed set SHALL be Core and below, including `Njord.Consensus`, and no other feature library

### Requirement: Base library registration of consensus
The architecture test infrastructure SHALL list `Njord.Consensus` as a base library alongside `Njord.Core`, `Njord.Weather`, `Njord.Sensors`, `Njord.Messages` and `Njord.Persistence`. It SHALL NOT list `Njord.Domain` or `Njord.Compute` — those assemblies no longer exist.

#### Scenario: BaseLibraryNames includes Consensus
- **WHEN** `NjordArchitecture.BaseLibraryNames` is inspected
- **THEN** it SHALL contain `"Njord.Consensus"` and SHALL NOT contain `"Njord.Compute"`

#### Scenario: BaseLibraryNames includes the split foundation libraries
- **WHEN** `NjordArchitecture.BaseLibraryNames` is inspected
- **THEN** it SHALL contain `"Njord.Weather"` and `"Njord.Sensors"`, and SHALL NOT contain `"Njord.Domain"`

### Requirement: Messages declares no compute logic
Types in `Njord.Messages` SHALL NOT have names ending in `Analyzer`, `Rule` or `Formulas`; `ZoneArchitectureSpec` SHALL enforce this so analysis logic stays in the feature libraries.

#### Scenario: Analyzer type in Messages
- **WHEN** a type named `*Analyzer` is declared in `Njord.Messages`
- **THEN** the architecture test run fails and names the type

## MODIFIED Requirements

### Requirement: Domain is independent of the zones
Types in the base libraries (`Njord.Consensus`, `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather`, `Njord.Sensors`) SHALL NOT depend on any feature library (`Njord.OpenMeteo`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Grpc`, `Njord.Pipeline`, `Njord.Mqtt`, `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) or on the `Njord` host; `ZoneArchitectureSpec` checks every base library.

#### Scenario: Domain references a transport type
- **WHEN** a type in `Njord.Weather` or `Njord.Sensors` gains a dependency on a type in `Njord.OpenMeteo`, `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

### Requirement: Foundation layers have no framework dependency
Types in the foundation layers (`Njord.Weather`, `Njord.Sensors`, `Njord.Consensus`, `Njord.Messages`, `Njord.Persistence`) SHALL NOT depend on any `Akka.*` namespace or package. `LayerReferenceSpec.Messages_has_no_Akka_dependency` SHALL enforce this for `Njord.Messages`, and `LayerReferenceSpec` SHALL enforce it for `Njord.Consensus`.

#### Scenario: Foundation layer gains an Akka dependency
- **WHEN** `Njord.Messages` or `Njord.Consensus` gains a reference to an `Akka*` assembly
- **THEN** the architecture test run fails

#### Scenario: Foundation layers currently pass
- **WHEN** the architecture tests run against the current code
- **THEN** they pass, with none of the foundation projects referencing any `Akka.*` package or namespace

## REMOVED Requirements

### Requirement: Layer reference direction
**Reason**: The reference direction changes with `Njord.Compute` replaced by `Njord.Consensus`.
**Migration**: See "Layer reference direction across consensus and messages".

### Requirement: Base library registration
**Reason**: `Njord.Compute` is no longer a base library; `Njord.Consensus` is.
**Migration**: See "Base library registration of consensus".
