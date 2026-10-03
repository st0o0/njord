## MODIFIED Requirements

### Requirement: Production classes are sealed
Every non-abstract class in every `Njord.*` production assembly SHALL be `sealed`, except compiler-generated types (such as the top-level-statements `Program` class).

#### Scenario: Unsealed production class
- **WHEN** a non-abstract, non-sealed class is added to any `Njord.*` production assembly
- **THEN** the architecture test run fails and lists the class

## ADDED Requirements

### Requirement: Feature libraries do not reference each other
`Njord.Pipeline`, `Njord.Egress`, `Njord.Enrichment`, `Njord.Mqtt`, `Njord.Grpc`, `Njord.Ingest` and `Njord.Sensors` SHALL depend only on `Njord.Core`, `Njord.Messages`, `Njord.Persistence` and `Njord.Domain` (never on each other, never on the `Njord` host). The reference direction SHALL be host → feature libraries → `Njord.Core` → `Njord.Messages`/`Njord.Persistence` → `Njord.Domain`.

#### Scenario: Lateral reference between feature libraries
- **WHEN** a type in one feature library gains a dependency on a type in another feature library
- **THEN** the architecture test run fails and names the offending type, the dependency and both assemblies

#### Scenario: Upward reference
- **WHEN** a type in `Njord.Core`, `Njord.Messages`, `Njord.Persistence` or `Njord.Domain` gains a dependency on a feature library or the host
- **THEN** the build or the architecture test run fails

#### Scenario: Enrichment does not know the egress protocol
- **WHEN** a type in `Njord.Enrichment` gains a dependency on `Njord.Mqtt` or `Njord.Grpc`
- **THEN** the architecture test run fails and names the offending type and dependency

### Requirement: Convention rules cover all production assemblies
The sealed-class convention SHALL be evaluated over all `Njord.*` production assemblies in one architecture load, and the test-class convention SHALL cover every test project.

#### Scenario: New library is covered automatically
- **WHEN** a new `Njord.*` production assembly is added to the solution and referenced by the host
- **THEN** its non-abstract classes are checked by the sealed rule without editing the rule
