# architecture-zone-enforcement Specification

## Purpose
Fail the test run when code violates njord's zone architecture (Ingest, Domain, Egress meet only in the domain model) or its sealed/Spec conventions, so the guardrails are enforced rather than remembered.
## Requirements
### Requirement: Ingest does not depend on Egress
Types in the Ingest zone (`Njord.Ingest`) SHALL NOT depend on types in the Egress-side namespaces (`Njord.Egress`, `Njord.Mqtt`, `Njord.Grpc`).

#### Scenario: Ingest references an egress type
- **WHEN** a type in `Njord.Ingest` gains a dependency on a type in `Njord.Egress`, `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

#### Scenario: Ingest depends only on the domain and shared infrastructure
- **WHEN** the architecture tests run against the current code
- **THEN** they pass, with `Njord.Ingest` depending only on `Njord.Domain.*`, `Njord.Configuration` and `Njord.Diagnostics`

### Requirement: Egress does not depend on Ingest
Types in the Egress-side namespaces (`Njord.Egress`, `Njord.Mqtt`, `Njord.Grpc`) SHALL NOT depend on types in `Njord.Ingest`.

#### Scenario: Egress references an ingest type
- **WHEN** a type in `Njord.Egress`, `Njord.Mqtt` or `Njord.Grpc` gains a dependency on a type in `Njord.Ingest`
- **THEN** the architecture test run fails and names the offending type and dependency

### Requirement: Domain is independent of the zones
Types in `Njord.Domain.*` SHALL NOT depend on `Njord.Ingest`, `Njord.Egress`, `Njord.Mqtt` or `Njord.Grpc`.

#### Scenario: Domain references a transport type
- **WHEN** a type in `Njord.Domain.Weather`, `Njord.Domain.Sensors` or `Njord.Domain.Analysis` gains a dependency on an Ingest or Egress-side type
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

### Requirement: Feature libraries do not reference each other
`Njord.Pipeline`, `Njord.Egress`, `Njord.Grpc`, `Njord.Ingest` and `Njord.Sensors` SHALL depend only on `Njord.Core`, `Njord.Messages`, `Njord.Persistence` and `Njord.Domain` (never on each other, never on the `Njord` host). The reference direction SHALL be host -> feature libraries -> `Njord.Core` -> `Njord.Messages`/`Njord.Persistence` -> `Njord.Domain`.

#### Scenario: Lateral reference between feature libraries
- **WHEN** a type in one feature library gains a dependency on a type in another feature library
- **THEN** the architecture test run fails and names the offending type, the dependency and both assemblies

#### Scenario: Egress does not use Pipeline types
- **WHEN** a type in `Njord.Egress` (for example `ModelStateActor`) gains a dependency on a type in `Njord.Pipeline`
- **THEN** the architecture test run fails; access to the pipeline goes through the `IPipelineActor` marker and the messages in `Njord.Messages`

#### Scenario: Upward reference
- **WHEN** a type in `Njord.Core`, `Njord.Messages`, `Njord.Persistence` or `Njord.Domain` gains a dependency on a feature library or the host
- **THEN** the build or the architecture test run fails

### Requirement: Convention rules cover all production assemblies
The sealed-class convention SHALL be evaluated over all `Njord.*` production assemblies in one architecture load, and the test-class convention SHALL cover every test project: `Njord.Architecture.Tests` SHALL load every `Njord.*Tests` assembly (and `Njord.Tests.Shared`) next to it.

#### Scenario: New library is covered automatically
- **WHEN** a new `Njord.*` production assembly is added to the solution and referenced by the host
- **THEN** its non-abstract classes are checked by the sealed rule without editing the rule

#### Scenario: New test project is covered automatically
- **WHEN** a new `Njord.<Name>.Tests` project is added next to the existing ones
- **THEN** `Njord.Architecture.Tests` picks it up through its `Njord.*.Tests` project reference pattern and the test-class and disabled-test rules check it without editing the rules

### Requirement: Persistence is configured before actors are registered
The host actor-system setup SHALL apply the persistence configuration (`WithSqlPersistence` or the selected provider) before registering any actor, so persistent actors find the journal and snapshot store when they start.

#### Scenario: Actors registered before persistence
- **WHEN** the actor registrations run before the persistence configuration in the production actor-system setup
- **THEN** the order-guard spec fails

#### Scenario: Persistent actors recover after extraction
- **WHEN** `Njord.Pipeline` is a separate assembly and the production setup runs with in-memory persistence
- **THEN** the persistent actors (`scheduler`, `budget-tracker`) start and every actor marker resolves from the registry

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

