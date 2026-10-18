# architecture-zone-enforcement Specification

## Purpose
Fail the test run when code violates njord's zone architecture (Ingest, Domain, Egress meet only in the domain model) or its sealed/Spec conventions, so the guardrails are enforced rather than remembered.
## Requirements
### Requirement: Domain is independent of the zones
Types in the base libraries (`Njord.Consensus`, `Njord.Core`, `Njord.Messages`, `Njord.Persistence`, `Njord.Weather`, `Njord.Sensors`) SHALL NOT depend on any feature library (`Njord.OpenMeteo`, `Njord.Budget`, `Njord.SensorHub`, `Njord.Grpc`, `Njord.Pipeline`, `Njord.Mqtt`, `Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`) or on the `Njord` host; `ZoneArchitectureSpec` checks every base library.

#### Scenario: Domain references a transport type
- **WHEN** a type in `Njord.Weather` or `Njord.Sensors` gains a dependency on a type in `Njord.OpenMeteo`, `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

### Requirement: Production classes are sealed
Every non-abstract class in every `Njord.*` production assembly SHALL be `sealed`, except compiler-generated types (such as the top-level-statements `Program` class).

#### Scenario: Unsealed production class
- **WHEN** a non-abstract, non-sealed class is added to any `Njord.*` production assembly
- **THEN** the architecture test run fails and lists the class

### Requirement: Test classes are sealed Spec classes
Every test class in every `Njord.*Tests` assembly (the per-library test projects, the host-resident `Njord.Tests` and `Njord.Architecture.Tests` itself) that contains tests SHALL be `sealed` and its name SHALL end in `Spec`.

#### Scenario: Test class violates the convention
- **WHEN** a class containing `[Fact]` or `[Theory]` methods is unsealed or not suffixed `Spec`
- **THEN** the architecture test run fails and lists the class

### Requirement: Convention rules cover all production assemblies
The sealed-class convention SHALL be evaluated over all `Njord.*` production assemblies in one architecture load, and the test-class convention SHALL cover every test project: `Njord.Architecture.Tests` SHALL load every `Njord.*Tests` assembly (and `Njord.Tests.Shared`) next to it.

#### Scenario: New library is covered automatically
- **WHEN** a new `Njord.*` production assembly is added to the solution and referenced by the host
- **THEN** its non-abstract classes are checked by the sealed rule without editing the rule

#### Scenario: New test project is covered automatically
- **WHEN** a new `Njord.<Name>.Tests` project is added next to the existing ones
- **THEN** `Njord.Architecture.Tests` picks it up through its `Njord.*.Tests` project reference pattern and the test-class and disabled-test rules check it without editing the rules

### Requirement: No disabled tests
Test methods in every test assembly (all `Njord.*Tests` projects, including `Njord.Architecture.Tests` itself, and `Njord.Tests.Shared`) SHALL NOT be disabled: no `Fact` or `Theory` attribute SHALL carry a non-empty `Skip` (or `SkipUnless`/`SkipWhen`), and no `Ignore` attribute SHALL be used. The check (`DisabledTestArchitectureSpec` in `Njord.Architecture.Tests`) SHALL cover public and non-public methods of all types, so it holds independent of the file naming that slopwatch's disabled-test rule inspects.

#### Scenario: Skipped test
- **WHEN** a test method declares `[Fact(Skip = "...")]` or `[Theory(Skip = "...")]`
- **THEN** the architecture test run fails and names the declaring type and method

#### Scenario: Ignored test
- **WHEN** a test method or class carries an `Ignore` attribute
- **THEN** the architecture test run fails and names the declaring type and method

#### Scenario: No disabled tests in the current code
- **WHEN** the architecture tests run against the current test assemblies
- **THEN** they pass

### Requirement: Foundation layers have no framework dependency
Types in the foundation layers (`Njord.Weather`, `Njord.Sensors`, `Njord.Consensus`, `Njord.Messages`, `Njord.Persistence`) SHALL NOT depend on any `Akka.*` namespace or package. `LayerReferenceSpec.Messages_has_no_Akka_dependency` SHALL enforce this for `Njord.Messages`, and `LayerReferenceSpec` SHALL enforce it for `Njord.Consensus`.

#### Scenario: Foundation layer gains an Akka dependency
- **WHEN** `Njord.Messages` or `Njord.Consensus` gains a reference to an `Akka*` assembly
- **THEN** the architecture test run fails

#### Scenario: Foundation layers currently pass
- **WHEN** the architecture tests run against the current code
- **THEN** they pass, with none of the foundation projects referencing any `Akka.*` package or namespace

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

