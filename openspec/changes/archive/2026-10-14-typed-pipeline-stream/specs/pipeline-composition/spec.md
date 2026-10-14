# pipeline-composition Delta

## MODIFIED Requirements

### Requirement: The analysis pipeline is composed from typed inline flows
The analysis pipeline SHALL be composed directly in `PipelineActor` as named
local `Flow` values — no `IAnalysisStage` interface or `AnalysisStage` class.
The pipeline SHALL use `AlsoTo` for scheduler feedback (not a Broadcast branch).
The analysis flow SHALL be a `GraphDSL` with `Broadcast<Forecast>(2)` splitting
into a `ForecastReceived` passthrough branch (no state) and an analysis branch
(`Scan` → consensus → feature fan-out). `ConsensusSnapshot` SHALL be the fan-out
type for the feature `Broadcast(N)`.

#### Scenario: Pipeline produces typed events without casts
- **WHEN** a `Forecast` enters the analysis flow
- **THEN** the output is a typed `PipelineEvent` variant (`ForecastReceived`, `ConsensusComputed`, `AlertComputed`, etc.) with no `object` wrapping or string discriminator

#### Scenario: ForecastReceived is emitted for every forecast
- **WHEN** a `Forecast` with `HasChanged = false` arrives
- **THEN** a `ForecastReceived` event is emitted and no analysis events follow

#### Scenario: Scan is only in the analysis branch
- **WHEN** a `Forecast` arrives
- **THEN** the `ForecastReceived` branch does not execute a Scan operator

## REMOVED Requirements

### Requirement: IAnalysisStage returns a composable Flow
Removed. The analysis pipeline is composed inline in `PipelineActor`, not
behind an `IAnalysisStage` interface.

#### Scenario: removed
- **WHEN** removed
- **THEN** removed
