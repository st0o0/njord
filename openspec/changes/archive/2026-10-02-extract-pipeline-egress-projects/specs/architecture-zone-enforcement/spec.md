## MODIFIED Requirements

### Requirement: Production classes are sealed
Every non-abstract class in every `Njord.*` production assembly SHALL be `sealed`, except compiler-generated types (such as the top-level-statements `Program` class).

#### Scenario: Unsealed production class
- **WHEN** a non-abstract, non-sealed class is added to any `Njord.*` production assembly
- **THEN** the architecture test run fails and lists the class

## ADDED Requirements

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
The sealed-class convention SHALL be evaluated over all `Njord.*` production assemblies in one architecture load, and the test-class convention SHALL cover every test project.

#### Scenario: New library is covered automatically
- **WHEN** a new `Njord.*` production assembly is added to the solution and referenced by the host
- **THEN** its non-abstract classes are checked by the sealed rule without editing the rule

### Requirement: Persistence is configured before actors are registered
The host actor-system setup SHALL apply the persistence configuration (`WithSqlPersistence` or the selected provider) before registering any actor, so persistent actors find the journal and snapshot store when they start.

#### Scenario: Actors registered before persistence
- **WHEN** the actor registrations run before the persistence configuration in the production actor-system setup
- **THEN** the order-guard spec fails

#### Scenario: Persistent actors recover after extraction
- **WHEN** `Njord.Pipeline` is a separate assembly and the production setup runs with in-memory persistence
- **THEN** the persistent actors (`scheduler`, `budget-tracker`) start and every actor marker resolves from the registry
