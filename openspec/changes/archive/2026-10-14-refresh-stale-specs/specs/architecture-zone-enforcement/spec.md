## ADDED Requirements

### Requirement: Feature libraries do not reference each other or the host
The feature libraries `Njord.OpenMeteo`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Grpc`, `Njord.Mqtt`, `Njord.Alerts`, `Njord.Derived`, `Njord.Trends` and `Njord.Indices` SHALL NOT depend on each other, on `Njord.Pipeline` or on the `Njord` host. `Njord.Pipeline` SHALL depend on no feature library other than the four analysis libraries, and never on the host. Feature libraries reach each other's actors only through the marker interfaces in `Njord.Core/Actors/ActorKeys.cs` and the messages in `Njord.Messages`.

#### Scenario: Lateral reference between feature libraries
- **WHEN** a type in one feature library gains a dependency on a type in another feature library
- **THEN** the architecture test run fails and names the offending type, the dependency and both assemblies

#### Scenario: Pipeline uses only the analysis libraries
- **WHEN** a type in `Njord.Pipeline` gains a dependency on a type in `Njord.Mqtt`, `Njord.Grpc`, `Njord.OpenMeteo`, `Njord.Budget` or `Njord.SensorHub`
- **THEN** the architecture test run fails; access to those actors goes through their markers and `Njord.Messages`

#### Scenario: Upward reference
- **WHEN** a type in `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather` or `Njord.Sensors` gains a dependency on a feature library or the host
- **THEN** the build or the architecture test run fails

#### Scenario: SensorHub is one of the non-cross-referencing feature libraries
- **WHEN** the architecture tests list the feature libraries that must not reference each other
- **THEN** `Njord.SensorHub` is included in that list

### Requirement: AkkaSetupContainer configures persistence before applying actor registrations
`AkkaSetupContainer.BuildSystem` SHALL apply the persistence configuration (`WithSqlPersistence` for the selected provider), remoting and clustering before it applies any `IActorRegistration`, so persistent actors find the journal and snapshot store when they start.

#### Scenario: Registrations run after the infrastructure
- **WHEN** `AkkaSetupContainer.BuildSystem` runs
- **THEN** `ConfigureInfrastructure` (persistence, remoting, clustering) completes before the first `IActorRegistration.Configure` call

## MODIFIED Requirements

### Requirement: Domain is independent of the zones
Types in the base libraries (`Njord.Compute`, `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather`, `Njord.Sensors`) SHALL NOT depend on any feature library (`Njord.OpenMeteo`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Grpc`, `Njord.Pipeline`, `Njord.Mqtt`, `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) or on the `Njord` host; `ZoneArchitectureSpec` checks every base library.

#### Scenario: Domain references a transport type
- **WHEN** a type in `Njord.Weather` or `Njord.Sensors` gains a dependency on a type in `Njord.OpenMeteo`, `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

### Requirement: Layer reference direction
Foundation libraries SHALL follow this reference direction: `Njord.Weather`, `Njord.Sensors`, and `Persistence` reference no Njord assembly; `Compute` references only `Njord.Weather` and `Njord.Sensors`; `Messages` references only `Njord.Weather`, `Njord.Sensors` and `Compute`; `Core` references only `Njord.Weather`, `Njord.Sensors`, `Messages`, `Persistence`, and `Compute`. Feature libraries reference only `Core` and below, except `Njord.Pipeline`, which may also reference the analysis libraries (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`). The host references all. `LayerReferenceSpec` SHALL check every assembly.

#### Scenario: Compute references only Weather and Sensors
- **WHEN** `LayerReferenceSpec` checks `Njord.Compute` assembly references
- **THEN** the only Njord references SHALL be `Njord.Weather` and `Njord.Sensors`

#### Scenario: Core references include Compute
- **WHEN** `LayerReferenceSpec` checks `Njord.Core` assembly references
- **THEN** the allowed Njord references SHALL be `Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, `Njord.Persistence`, and `Njord.Compute`

#### Scenario: Feature libraries reference Core and below
- **WHEN** `LayerReferenceSpec` checks a feature library's assembly references
- **THEN** the allowed set SHALL include `Njord.Compute` (transitively via Core and below)

### Requirement: Foundation layers have no framework dependency
Types in the foundation layers (`Njord.Weather`, `Njord.Sensors`, `Njord.Messages`, `Njord.Persistence`) SHALL NOT depend on any `Akka.*` namespace or package. `LayerReferenceSpec.Messages_has_no_Akka_dependency` SHALL enforce this for `Njord.Messages`.

#### Scenario: Foundation layer gains an Akka dependency
- **WHEN** `Njord.Messages` gains a reference to an `Akka*` assembly
- **THEN** the architecture test run fails

#### Scenario: Foundation layers currently pass
- **WHEN** the architecture tests run against the current code
- **THEN** they pass, with none of the four foundation projects referencing any `Akka.*` package or namespace

## REMOVED Requirements

### Requirement: Ingest does not depend on Egress
**Reason**: There is no `Njord.Ingest` or `Njord.Egress` assembly any more. The Open-Meteo side is `Njord.OpenMeteo` and the consumers are `Njord.Mqtt` and `Njord.Grpc`; all of them are feature libraries kept apart by the lateral rule in `ZoneArchitectureSpec`.
**Migration**: See "Feature libraries do not reference each other or the host".

### Requirement: Egress does not depend on Ingest
**Reason**: There is no `Njord.Ingest` or `Njord.Egress` assembly any more; the separation is enforced by the lateral feature-library rule.
**Migration**: See "Feature libraries do not reference each other or the host".

### Requirement: Feature libraries do not reference each other
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Feature libraries do not reference each other or the host" in this capability.

### Requirement: Persistence is configured before actors are registered
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "AkkaSetupContainer configures persistence before applying actor registrations" in this capability.

### Requirement: Enrichment and Mqtt follow the library reference rules
**Reason**: `Njord.Enrichment` no longer exists; `Njord.Mqtt` and the analysis libraries are covered by the general lateral feature-library rule.
**Migration**: See "Feature libraries do not reference each other or the host".
