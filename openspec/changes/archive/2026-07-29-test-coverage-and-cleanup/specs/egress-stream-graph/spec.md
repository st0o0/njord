## MODIFIED Requirements

### Requirement: ModelStateActor consumes FetchOutcome from Pipeline SourceRef
The `ModelStateActor` SHALL consume `FetchOutcome` elements from the pipeline's `SourceRef<FetchOutcome>`. It SHALL import `FetchOutcome` from `Njord.Domain.Weather`, not from `Njord.Ingest`. The Egress zone SHALL NOT reference the Ingest namespace.

#### Scenario: FetchOutcome.Success produces PerModelUpdate
- **WHEN** a FetchOutcome.Success arrives via the pipeline SourceRef
- **THEN** the ModelStateActor emits an EgressEvent.PerModelUpdate downstream

#### Scenario: No Ingest namespace import in Egress
- **WHEN** the codebase is compiled
- **THEN** no file under Njord.Egress contains `using Njord.Ingest`
