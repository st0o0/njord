## Purpose

Keeps the production project structurally consistent: one type per source file and data records free of computation logic.
## Requirements
### Requirement: One public or internal type per source file

Each `.cs` file in the host project (`src/Njord`) SHALL contain exactly one public or internal type (class, record, enum, or struct). The filename SHALL match the type name. Exception: Akka actor message records MAY colocate with their actor class.

#### Scenario: File matches type

- **WHEN** a file `ScoreEnvelope.cs` exists
- **THEN** it SHALL contain exactly one type named `ScoreEnvelope`

#### Scenario: Actor messages colocated

- **WHEN** `ConfigManager.cs` contains private message records used only by that actor
- **THEN** this SHALL NOT be flagged as a violation

### Requirement: Result records carry no computation logic
Analysis result records (`IndexResult`, `ConsensusSnapshot`, `TrendResult`, `DerivedResult`, `AlertResult`) SHALL be pure data carriers with no static `Compute` factory methods. Computation SHALL live in the analyzers (`AlertAnalyzer`, `DerivedAnalyzer`, `TrendAnalyzer`, `IndexAnalyzer`) and `ConsensusBuilder`, which are registered in DI.

#### Scenario: IndexResult has no Compute method
- **WHEN** `IndexResult` is inspected
- **THEN** it SHALL have no static methods

#### Scenario: Analyzer is injectable
- **WHEN** `ForecastPipeline` needs to compute index scores
- **THEN** it receives an `IndexAnalyzer` via constructor injection

