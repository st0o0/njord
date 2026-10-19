## MODIFIED Requirements

### Requirement: One stateful stage computes all analysis per forecast
For every forecast the `StatefulSelectMany` stage SHALL update a `ModelForecasts` held in the stage closure, build a `LocationConsensus` with `ConsensusBuilder.Create(forecasts, location, settings.Consensus)`, and emit, in this order: `PipelineEvent.ForecastReceived`, `PipelineEvent.ConsensusComputed` (only while `ConsensusOptions.Enabled` is true; the `ModelForecasts` is built either way), then `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed` for each analyzer that returns a result. Each analyzer SHALL take the `LocationConsensus` directly plus its own extras and the current settings record. There SHALL be no `PollContext` record and no per-feature input interfaces. An analyzer whose feature is disabled SHALL return `null`, and no event SHALL be emitted for it. All analysis state SHALL live in the stage closure, not in actor fields. Analysis settings SHALL be read from `IOptions<T>` at pipeline construction and SHALL remain fixed for the pipeline incarnation.

#### Scenario: Events per forecast
- **WHEN** a forecast arrives and consensus, alerts, derived, trends and indices are enabled and a previous consensus exists
- **THEN** the stage emits `ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed`

#### Scenario: Disabled feature emits nothing
- **WHEN** the alerts feature is disabled
- **THEN** `AlertAnalyzer.Compute` returns `null` and no `AlertComputed` event is emitted

#### Scenario: Analyzer signatures
- **WHEN** an analyzer is invoked by the pipeline
- **THEN** it receives a `LocationConsensus` (plus `previous` for trends and an optional `SensorReadings` for indices) and its settings record, never a context object

## REMOVED Requirements

### Requirement: Settings side channel
**Reason**: No runtime config changes. Analysis settings are read from IOptions<T> at pipeline construction and remain fixed for the pipeline incarnation. The Source.ActorRef side channel, RepeatPrevious, QueryAnalysisSettings to ConfigManager, and AnalysisSettingsChanged forwarding are all removed.
**Migration**: Settings are constructor-injected. A settings change requires a container restart, which restarts the pipeline with the new values.
