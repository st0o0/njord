## MODIFIED Requirements

### Requirement: PipelineActor owns the full stream graph

`PipelineActor` SHALL materialize the complete stream graph from fetch through
feature fan-out to a `BroadcastHub<PipelineEvent>` endpoint. The analysis stage
(consensus computation, PollContext assembly, feature invocation) SHALL be
provided via an injected `IAnalysisStage` interface defined in `Njord.Core`,
so `PipelineActor` in `Njord.Pipeline` does not reference feature libraries.

#### Scenario: Pipeline graph structure
- **WHEN** `PipelineActor` materializes the graph
- **THEN** the graph follows: Fetch → Consensus → Scan (previous) →
  Build PollContext → Broadcast → [Feature Flows] → Merge →
  BroadcastHub, with the analysis stage injected via `IAnalysisStage`

#### Scenario: Feature invocation in pipeline
- **WHEN** the pipeline invokes a stateless feature (Alerts, Derived, Trends, Indices)
- **THEN** it calls the feature's compute method via `Flow.Select()` with the
  `PollContext` typed as that feature's input interface

#### Scenario: History invocation in pipeline
- **WHEN** the pipeline invokes the History feature
- **THEN** it uses `Flow.SelectAsync()` with an Ask to the
  `ForecastHistoryActor` for record + query + compute

#### Scenario: PipelineActor is a lifecycle owner only
- **WHEN** the `PipelineActor` is inspected
- **THEN** it contains no business logic and no state beyond graph lifecycle
  management (materialize, keep alive, restart on failure)

#### Scenario: Analysis stage is injected
- **WHEN** `PipelineActor` is constructed
- **THEN** it receives an `IAnalysisStage` via DI that provides a
  `Flow<ModelForecast, IReadOnlyList<PipelineEvent>, NotUsed>` to plug into
  the stream graph

## ADDED Requirements

### Requirement: IAnalysisStage decouples pipeline from feature libraries

`Njord.Core` SHALL define an `IAnalysisStage` interface with a method that
returns a Flow converting `ModelForecast` inputs into `PipelineEvent` outputs.
The Host SHALL implement this interface, wiring `ConsensusSnapshotFactory`,
`ModelSnapshot` accumulation via Scan, `PollContext` construction, and all
feature analyzers. `PipelineActor` receives the implementation via DI.

#### Scenario: Analysis stage produces analysis results
- **WHEN** a `ModelForecast` enters the analysis stage
- **THEN** it accumulates into a `ModelSnapshot`, computes consensus, runs all
  enabled features, and emits `PipelineEvent.AnalysisResult` for each non-null
  feature result plus `PipelineEvent.ModelForecastUpdate`

#### Scenario: Disabled feature produces no event
- **WHEN** a feature is disabled via configuration
- **THEN** the analysis stage skips it and emits no `AnalysisResult` for that
  feature

#### Scenario: First cycle has no previous consensus
- **WHEN** the first `ModelForecast` arrives
- **THEN** the analysis stage produces consensus but `ITrendInput.Previous` is
  null, so the trend analyzer emits no result
