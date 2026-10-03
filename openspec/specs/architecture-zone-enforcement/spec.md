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
Every non-abstract class in the `Njord` assembly SHALL be `sealed`.

#### Scenario: Unsealed production class
- **WHEN** a non-abstract, non-sealed class is added to `Njord`
- **THEN** the architecture test run fails and lists the class

### Requirement: Test classes are sealed Spec classes
Every test class in `Njord.Tests` that contains tests SHALL be `sealed` and its name SHALL end in `Spec`.

#### Scenario: Test class violates the convention
- **WHEN** a class containing `[Fact]` or `[Theory]` methods is unsealed or not suffixed `Spec`
- **THEN** the architecture test run fails and lists the class

