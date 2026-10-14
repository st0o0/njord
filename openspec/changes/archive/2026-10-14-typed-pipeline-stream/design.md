# Design: typed-pipeline-stream

## D1: Rename ModelForecast → Forecast

`ModelForecast` carries a `WeatherModel Model` property — the "Model" prefix is
redundant. Rename to `Forecast` throughout (`Njord.Weather`). This touches every
project that references it but is a mechanical rename.

## D2: Rename FetchOutcome → PollOutcome

"Fetch" is an implementation detail; the domain concept is "polling" weather
models. Rename to `PollOutcome` with variants `PollOutcome.Success` and
`PollOutcome.Failure` (`Njord.Messages.Pipeline`).

## D3: Add AccumulatedForecast record

`AccumulatedForecast(ModelSnapshot Snapshot, Forecast Forecast)` in
`Njord.Weather`. The output type of the Scan that accumulates forecasts into
a snapshot. `Snapshot.HasChanged` gates the analysis path.

## D4: Move PipelineEvent to Njord.Core and add typed variants

`Njord.Messages` can only reference `Njord.Weather` and `Njord.Sensors`
(architecture rule). The typed variants need `Njord.Compute.Analysis` types
(`AlertResult`, `ConsensusSnapshot`, etc.). `Njord.Core` references both
`Njord.Messages` and `Njord.Compute` — it is the natural home.

Move `PipelineEvent` from `Njord.Messages.Egress` to `Njord.Core.Egress`.
Replace `AnalysisResult(string TypeName, object Result, DateTimeOffset?)` with:

- `ForecastReceived(string Location, WeatherModel Model, Forecast Forecast)` —
  replaces `ModelForecastUpdate`
- `ConsensusComputed(ConsensusSnapshot Consensus, DateTimeOffset Timestamp)`
- `AlertComputed(AlertResult Result, DateTimeOffset Timestamp)`
- `DerivedComputed(DerivedResult Result, DateTimeOffset Timestamp)`
- `TrendComputed(TrendResult Result, DateTimeOffset Timestamp)`
- `IndexComputed(IndexResult Result, DateTimeOffset Timestamp)`

`ModelCapability` stays unchanged. Each result type already carries `Location`.

## D5: Analyzer signatures take ConsensusSnapshot directly

Remove `IAlertInput`, `IDerivedInput`, `ITrendInput`, `IIndexInput` and
`PollContext`. Change analyzer Compute methods:

- `AlertAnalyzer.Compute(ConsensusSnapshot) → AlertResult?`
- `DerivedAnalyzer.Compute(ConsensusSnapshot) → DerivedResult?` — Horizons
  becomes a DI-injected field on the analyzer
- `TrendAnalyzer.Compute(ConsensusSnapshot, ConsensusSnapshot?) → TrendResult?`
- `IndexAnalyzer.Compute(ConsensusSnapshot, SensorSnapshot?) → IndexResult?`

## D6: Pipeline composition with AlsoTo and inline flows

Remove `IAnalysisStage`, `AnalysisStage`, `PassthroughAnalysisStage`. The
pipeline is composed directly in `PipelineActor.MaterializePipeline` as named
local flows:

```
queueSource
  .Via(fetch)                              WeightedTarget → PollOutcome
  .AlsoTo(schedulerFeedback, false)        side channel → Scheduler
  .Collect(Success → Forecast)             PollOutcome → Forecast
  .Via(analysis)                           Forecast → PipelineEvent
  .RunWith(pipelineOutputSink)
```

The `analysis` flow is a GraphDSL with `Broadcast<Forecast>(2)` /
`Merge<PipelineEvent>(2)`:
- Branch 0: `Select(→ ForecastReceived)` — always, no state
- Branch 1: `Scan(AccumulatedForecast)` → `Where(HasChanged)` →
  `Select(Factory.Create → ConsensusSnapshot)` → feature fan-out

## D7: Feature fan-out with per-branch state

The feature fan-out is a nested `Broadcast<ConsensusSnapshot>(N)` /
`Merge<PipelineEvent>(N)` inside the analysis branch. Each branch gets
`ConsensusSnapshot` and independently acquires its extra data:

- **Consensus branch:** `Select(→ ConsensusComputed)` — pure
- **Alerts branch:** `Select(alerts.Compute)` → `Where(notNull)` →
  `Select(→ AlertComputed)` + supervision
- **Derived branch:** `Select(derived.Compute)` → `Where(notNull)` →
  `Select(→ DerivedComputed)` + supervision
- **Trends branch:** `Scan(previous)` → `Select(trends.Compute)` →
  `Where(notNull)` → `Select(→ TrendComputed)` + supervision
- **Indices branch:** `SelectAsync(QuerySensors)` →
  `Select(indices.Compute)` → `Where(notNull)` →
  `Select(→ IndexComputed)` + supervision

Scan for previous consensus is only in the Trends branch. SensorHub Ask is
only in the Indices branch. No global state accumulation beyond the
ModelSnapshot Scan.

## D8: Consumer migration

MqttStateActor and GrpcConsumerActor switch from
`(TypeName, Result) switch` to typed pattern matching:

```csharp
PipelineEvent.ConsensusComputed c => ...,
PipelineEvent.AlertComputed a => ...,
```

`EnrichmentProtoMapper.MapToEvent` changes from
`(string typeName, object result)` to typed overloads or a single method
that pattern-matches on `PipelineEvent`.

## D9: Test strategy

- Analyzer tests: simpler — pass `ConsensusSnapshot` directly, no
  `TestAlertInput` etc.
- Pipeline tests: inject analyzers with `enabled: false` — only
  `ForecastReceived` and `ConsensusComputed` events emitted.
- Consumer tests: update verified snapshots for new event types.
- Architecture tests: remove `IAnalysisStage` from any checks.
