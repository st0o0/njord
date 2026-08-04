## ADDED Requirements

### Requirement: One public or internal type per source file

Each `.cs` file in the production project SHALL contain exactly one public or internal type (class, record, enum, or struct). The filename SHALL match the type name. Exception: Akka actor message records MAY colocate with their actor class.

#### Scenario: File matches type

- **WHEN** a file `ScoreEnvelope.cs` exists
- **THEN** it SHALL contain exactly one type named `ScoreEnvelope`

#### Scenario: Actor messages colocated

- **WHEN** `MqttConnectionActor.cs` contains message records used only by that actor
- **THEN** this SHALL NOT be flagged as a violation

### Requirement: Data records carry no computation logic

Domain result records (`IndexResult`, `ConsensusSnapshot`, `TrendResult`, `DerivedResult`, `HistoryResult`) SHALL be pure data carriers with no static `Compute` factory methods. Computation SHALL live in dedicated service classes registered in DI.

#### Scenario: IndexResult has no Compute method

- **WHEN** `IndexResult` is inspected
- **THEN** it SHALL have no static methods

#### Scenario: Computer service is injectable

- **WHEN** `IndexEnrichment` needs to compute index scores
- **THEN** it SHALL receive an `IndexComputer` via constructor injection
