## MODIFIED Requirements

### Requirement: Consensus output is independently toggleable
`ForecastPipeline` SHALL always compute the `ConsensusSnapshot`, regardless of `ConsensusOptions.Enabled`, because all analyzers depend on it. The `Enabled` flag SHALL control only whether `PipelineEvent.ConsensusComputed` is emitted to the consumers (MQTT state and gRPC). With the flag off, the other analysis features SHALL be computed and emitted exactly as with the flag on, and trends SHALL still be compared against the previous consensus snapshot. The flag SHALL be read when the pipeline starts: like the other feature `Enabled` toggles it is restart-required, so a runtime change is persisted and takes effect after the next start.

#### Scenario: Consensus always computed for analysis
- **WHEN** `ConsensusOptions.Enabled` is false
- **THEN** the `ConsensusSnapshot` is still computed and passed to the analyzers, but no `ConsensusComputed` event is emitted

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
