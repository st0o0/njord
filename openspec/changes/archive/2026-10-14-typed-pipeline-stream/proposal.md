# Proposal: typed-pipeline-stream

## Problem

The pipeline stream has three problems:

1. **Untyped analysis results.** `PipelineEvent.AnalysisResult` wraps results as
   `object` with a `string TypeName` discriminator. Consumers double-dispatch on
   `(string, object)` with casts. The stream uses `Func<PollContext, object?>` to
   invoke analyzers.

2. **God-object input.** `PollContext` has 6 properties and implements 4 interfaces,
   but each feature uses only 1-2 of them. `Previous` is only for Trends,
   `Sensors` only for Indices, `Horizons` is a DI constant only Derived needs,
   `Snapshot` is not used by any feature directly.

3. **Monolithic AnalysisStage.** `AnalysisStage` wraps Scan + SelectAsync +
   Scan + GraphDSL in one class with 4 internal records (`SnapshotOutput`,
   `CycleInput`, `PreviousAccum`, `CycleOutput`). Every feature branch checks
   `Where(context is not null)` because unchanged forecasts flow through the
   entire graph.

## Solution

Replace the untyped pipeline with fully typed flows composed directly in
`PipelineActor.MaterializePipeline`:

- **Rename** `FetchOutcome` → `PollOutcome`, `ModelForecast` → `Forecast`,
  `ModelForecastUpdate` → `ForecastReceived`.
- **Replace** `AnalysisResult(string, object)` with typed events:
  `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed`,
  `IndexComputed`.
- **Add** `AccumulatedForecast(ModelSnapshot, Forecast)` as the Scan output type.
- **Remove** `PollContext`, `IAlertInput`, `IDerivedInput`, `ITrendInput`,
  `IIndexInput`, `AnalysisStage`, `IAnalysisStage`, `PassthroughAnalysisStage`.
- **Change** analyzer signatures to take `ConsensusSnapshot` directly (plus
  per-feature extras: `Previous` for Trends, `Sensors` for Indices).
- **Use** `AlsoTo(propagateFailure: false)` for scheduler feedback instead of
  a Broadcast branch.
- **Move** the Trends Scan and Indices SensorHub Ask into their respective
  branches instead of running them globally.

## Non-goals

- Changing what features compute (AlertResult, DerivedResult etc. stay as-is).
- Changing the ConsensusSnapshot/ModelSnapshot domain model.
- Changing the consumer architecture (MqttStateActor, GrpcConsumerActor).
- Adding new features or analysis types.
- API-budget changes (no polling changes).
