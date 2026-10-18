# hourly-consensus Specification

## Purpose

Hourly consensus as a facet of the `LocationConsensus` built by `ConsensusBuilder` for every forecast: it covers every hour with sufficient model coverage, is exposed through the `ConsensusUpdate` proto message, is toggled for consumers by `ConsensusOptions.Enabled`, and is published per horizon to MQTT.
## Requirements
### Requirement: gRPC output uses existing ConsensusUpdate proto message
The consensus SHALL be mapped to a `ConsensusUpdate` proto message and exposed via the `consensus` field of `GetEnrichmentsResponse` (field 7) and of the `EnrichmentEvent` payload oneof (field 15). There is no separate `hourly_consensus` field.

#### Scenario: GetEnrichments returns consensus
- **WHEN** a gRPC client calls `GetEnrichments` for a location with consensus enabled
- **THEN** the response SHALL contain a `consensus` field with hourly `HorizonConsensus` entries

#### Scenario: StreamEnrichments emits consensus events
- **WHEN** consensus is computed after a poll cycle
- **THEN** an `EnrichmentEvent` with `type_name = "consensus"` and `consensus` payload SHALL be emitted

### Requirement: Hourly consensus covers every hour with sufficient model coverage
Hourly consensus SHALL be computed by `ConsensusBuilder.Create` as the `HourlyConsensus` facet of the `LocationConsensus`; it SHALL NOT be a separate analysis feature. The computation iterates the hourly parameters, collects model values per horizon via exact point lookup and calls `ConsensusStatistics`.

#### Scenario: Hourly consensus across models with different horizons
- **WHEN** `ConsensusBuilder.Create` is called with 3 models having horizons 48h, 72h, and 120h
- **THEN** `Hourly.Parameters` contains consensus for hours 0 through 72 (cutoff = second-to-last)

#### Scenario: Single model remaining stops consensus
- **WHEN** only 1 model has hourly data for a location
- **THEN** `Hourly.CutoffHour` is -1 and `Hourly.Parameters` is empty

#### Scenario: 3-hourly models contribute at their native hours only
- **WHEN** a model provides data at 3-hour intervals (h0, h3, h6...)
- **THEN** that model contributes only at those hours; other hours have one fewer contributing model

### Requirement: Consensus output is independently toggleable
`ForecastPipeline` SHALL always compute the `LocationConsensus`, regardless of `ConsensusOptions.Enabled`, because all analyzers depend on it. The `Enabled` flag SHALL control only whether `PipelineEvent.ConsensusComputed` is emitted to the consumers (MQTT state and gRPC). With the flag off, the other analysis features SHALL be computed and emitted exactly as with the flag on, and trends SHALL still be compared against the previous `LocationConsensus`. The flag SHALL be read when the pipeline starts: like the other feature `Enabled` toggles it is restart-required, so a runtime change is persisted and takes effect after the next start.

#### Scenario: Consensus always computed for analysis
- **WHEN** `ConsensusOptions.Enabled` is false
- **THEN** the `LocationConsensus` is still computed and passed to the analyzers, but no `ConsensusComputed` event is emitted

#### Scenario: Consensus output controlled by Enabled flag
- **WHEN** `ConsensusOptions.Enabled` is true
- **THEN** a `PipelineEvent.ConsensusComputed` is emitted for every forecast

#### Scenario: Other analyses are emitted while consensus is disabled
- **WHEN** `ConsensusOptions.Enabled` is false and alerts, derived values, trends and indices are enabled
- **THEN** every forecast still produces its alert, derived and index events, and every forecast after the first produces a trend event

#### Scenario: Disabled consensus reaches no consumer
- **WHEN** `ConsensusOptions.Enabled` is false and a client calls `GetEnrichments` after a poll cycle
- **THEN** the response carries the enabled analyses but no `consensus` field, and no consensus state is published to MQTT

#### Scenario: Toggling consensus at runtime requires a restart
- **WHEN** `ConsensusOptions.Enabled` is changed at runtime
- **THEN** the change is reported as restart-required and the running pipeline keeps its startup value for the consensus output

### Requirement: Consensus state is published per horizon
`MqttStatePublisher` SHALL publish one retained MQTT message per hourly horizon to `{baseTopic}/{location}/consensus/h{N}` and one per daily horizon to `{baseTopic}/{location}/consensus/d{N}`, built by `StatePayloadBuilder.FromConsensus`.

#### Scenario: MQTT topics for consensus
- **WHEN** a `ConsensusComputed` event with `Hourly.Parameters` reaches the MQTT publisher
- **THEN** one retained MQTT message per hour horizon is published to `{baseTopic}/{location}/consensus/h{N}`

