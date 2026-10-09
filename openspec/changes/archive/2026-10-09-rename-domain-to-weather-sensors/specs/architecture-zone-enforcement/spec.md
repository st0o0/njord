## MODIFIED Requirements

### Requirement: Ingest does not depend on Egress
Types in the Ingest zone (`Njord.Ingest`) SHALL NOT depend on types in the Egress-side namespaces (`Njord.Egress`, `Njord.Mqtt`, `Njord.Grpc`).

#### Scenario: Ingest references an egress type
- **WHEN** a type in `Njord.Ingest` gains a dependency on a type in `Njord.Egress`, `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Ingest depends only on the domain and shared infrastructure
- **WHEN** the architecture tests run against the current code
- **THEN** they pass, with `Njord.Ingest` depending only on `Njord.Weather`, `Njord.Configuration` and `Njord.Diagnostics`

### Requirement: Domain is independent of the zones
Types in `Njord.Weather.*` and `Njord.Sensors.*` (the foundation libraries) SHALL NOT depend on `Njord.Ingest`, `Njord.Egress`, `Njord.Mqtt`, `Njord.Grpc` or `Njord.SensorHub`.

#### Scenario: Domain references a transport type
- **WHEN** a type in `Njord.Weather` or `Njord.Sensors` gains a dependency on an Ingest or Egress-side type
- **THEN** the architecture test run fails and names the offending type and dependency

### Requirement: Layer reference direction
Foundation libraries SHALL follow this reference direction: `Njord.Weather`, `Njord.Sensors`, and `Persistence` reference no Njord assembly; `Messages` references only `Njord.Weather` and `Njord.Sensors`; `Compute` references only `Njord.Weather` and `Njord.Sensors`; `Core` references only `Njord.Weather`, `Njord.Sensors`, `Messages`, `Persistence`, and `Compute`. Feature libraries reference only `Core` and below. The host references all.

#### Scenario: Compute references only Weather and Sensors
- **WHEN** `LayerReferenceSpec` checks `Njord.Compute` assembly references
- **THEN** the only Njord references SHALL be `Njord.Weather` and `Njord.Sensors`

#### Scenario: Core references include Compute
- **WHEN** `LayerReferenceSpec` checks `Njord.Core` assembly references
- **THEN** the allowed Njord references SHALL be `Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, `Njord.Persistence`, and `Njord.Compute`

#### Scenario: Feature libraries reference Core and below
- **WHEN** `LayerReferenceSpec` checks a feature library's assembly references
- **THEN** the allowed set SHALL include `Njord.Compute` (transitively via Core and below)

### Requirement: Base library registration
The architecture test infrastructure SHALL list `Njord.Compute` as a base library alongside `Njord.Core`, `Njord.Weather`, `Njord.Sensors`, and `Njord.Persistence`. It SHALL NOT list `Njord.Domain` — that assembly no longer exists.

#### Scenario: BaseLibraryNames includes Compute
- **WHEN** `NjordArchitecture.BaseLibraryNames` is inspected
- **THEN** it SHALL contain `"Njord.Compute"`

#### Scenario: BaseLibraryNames includes the split foundation libraries
- **WHEN** `NjordArchitecture.BaseLibraryNames` is inspected
- **THEN** it SHALL contain `"Njord.Weather"` and `"Njord.Sensors"`, and SHALL NOT contain `"Njord.Domain"`

### Requirement: Feature libraries do not reference each other
`Njord.Pipeline`, `Njord.Egress`, `Njord.Grpc`, `Njord.Ingest` and `Njord.SensorHub` SHALL depend only on `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather` and `Njord.Sensors` (never on each other, never on the `Njord` host). The reference direction SHALL be host -> feature libraries -> `Njord.Core` -> `Njord.Messages`/`Njord.Persistence` -> `Njord.Weather`/`Njord.Sensors`.

#### Scenario: Lateral reference between feature libraries
- **WHEN** a type in one feature library gains a dependency on a type in another feature library
- **THEN** the architecture test run fails and names the offending type, the dependency and both assemblies

#### Scenario: Egress does not use Pipeline types
- **WHEN** a type in `Njord.Egress` (for example `ModelStateActor`) gains a dependency on a type in `Njord.Pipeline`
- **THEN** the architecture test run fails; access to the pipeline goes through the `IPipelineActor` marker and the messages in `Njord.Messages`

#### Scenario: Upward reference
- **WHEN** a type in `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather` or `Njord.Sensors` gains a dependency on a feature library or the host
- **THEN** the build or the architecture test run fails

#### Scenario: SensorHub is one of the non-cross-referencing feature libraries
- **WHEN** `LayerReferenceSpec` lists feature libraries that must not reference each other
- **THEN** `Njord.SensorHub` is included in that list in place of the former `Njord.Sensors` feature library

### Requirement: Enrichment and Mqtt follow the library reference rules
`Njord.Enrichment` and `Njord.Mqtt` SHALL depend only on `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather` and `Njord.Sensors` (never on each other, never on any other feature library, never on the `Njord` host). `Njord.Enrichment` SHALL NOT contain Home Assistant or MQTT knowledge.

#### Scenario: Enrichment does not know the egress protocol
- **WHEN** a type in `Njord.Enrichment` gains a dependency on `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Mqtt does not know enrichment features
- **WHEN** a type in `Njord.Mqtt` gains a dependency on a type in `Njord.Enrichment` (for example `IEnrichmentFeature`)
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Lateral reference between feature libraries
- **WHEN** a type in `Njord.Enrichment` or `Njord.Mqtt` gains a dependency on a type in another feature library
- **THEN** the architecture test run fails and names the offending type, the dependency and both assemblies
