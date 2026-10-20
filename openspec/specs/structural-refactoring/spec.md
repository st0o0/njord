## Purpose

Keeps the production project structurally consistent: one type per source file and data records free of computation logic.
## Requirements
### Requirement: One public or internal type per source file

Each `.cs` file in production handler projects (`src/Njord.Alerts`, `src/Njord.Scores`, `src/Njord.Insights`, `src/Njord.Advisories`) SHALL contain exactly one public or internal type (class, record, enum, or struct). The filename SHALL match the type name. Exception: Akka actor message records MAY colocate with their actor class.

#### Scenario: File matches type

- **WHEN** a file `ScoreEnvelope.cs` exists
- **THEN** it SHALL contain exactly one type named `ScoreEnvelope`

#### Scenario: Actor messages colocated

- **WHEN** `ConfigManager.cs` contains private message records used only by that actor
- **THEN** this SHALL NOT be flagged as a violation

