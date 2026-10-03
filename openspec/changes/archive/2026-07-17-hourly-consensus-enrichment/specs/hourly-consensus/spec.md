## ADDED Requirements

### Requirement: HourlyConsensusEnrichment computes consensus for every hour with sufficient model coverage
`HourlyConsensusEnrichment` SHALL implement `IStatelessEnrichment` with `TypeName = "hourly-consensus"`. On each `ModelSnapshot`, it SHALL compute `ConsensusResult` for hourly horizons from h0 up to the last hour where at least 2 models have forecast data. Hours where fewer than 2 models contribute data for a given parameter SHALL be excluded from the output.

#### Scenario: Hourly consensus across models with different horizons
- **WHEN** a `ModelSnapshot` contains icon_d2 (48h coverage) and ecmwf_ifs025 (240h coverage) and gfs_seamless (384h coverage)
- **THEN** the enrichment SHALL produce consensus for h0 through h48 (the last hour where ≥2 models have data)
- **AND** hours like h1, h2, h4, h5 (where only hourly-resolution models contribute) SHALL still be included if ≥2 hourly models are available

#### Scenario: Single model remaining stops consensus
- **WHEN** only one model has data beyond h48
- **THEN** the enrichment SHALL NOT produce consensus entries for h49 and beyond

#### Scenario: 3-hourly models contribute at their native hours
- **WHEN** ecmwf_ifs025 provides data at h0, h3, h6, h9... (3-hourly)
- **THEN** it SHALL be included in consensus at h3, h6, h9... via the existing ±30min tolerance window
- **AND** it SHALL NOT contribute to h1, h2, h4, h5... (no data within tolerance)

### Requirement: HourlyConsensusEnrichment is independently toggleable
The enrichment SHALL be controlled by `EnrichmentOptions.HourlyConsensus.Enabled` (default `false`). It SHALL operate independently of the existing `ConsensusEnrichment` — both can be enabled or disabled independently.

#### Scenario: Disabled by default
- **WHEN** no configuration override is provided
- **THEN** the enrichment SHALL be disabled and produce no output

#### Scenario: Enabled produces output
- **WHEN** `HourlyConsensus.Enabled = true`
- **THEN** the enrichment SHALL compute and emit hourly consensus on each poll cycle

### Requirement: gRPC output uses existing ConsensusUpdate proto message
The hourly consensus SHALL be mapped to a `ConsensusUpdate` proto message (same type as the horizon-based consensus) and exposed via a dedicated field in `GetEnrichmentsResponse` and `EnrichmentEvent`.

#### Scenario: GetEnrichments returns hourly consensus
- **WHEN** a gRPC client calls `GetEnrichments` for a location with hourly consensus enabled
- **THEN** the response SHALL contain a `hourly_consensus` field with `ConsensusUpdate` containing one `ParameterConsensus` per parameter, each with `HorizonConsensus` entries for every valid hour

#### Scenario: StreamEnrichments emits hourly consensus events
- **WHEN** hourly consensus is computed after a poll cycle
- **THEN** a `EnrichmentEvent` with `type_name = "hourly-consensus"` and `hourly_consensus` payload SHALL be emitted on the stream

### Requirement: MQTT output publishes one topic per hour
The enrichment SHALL publish retained MQTT state messages following the pattern `{baseTopic}/{location}/hourly-consensus/h{N}` for each valid hour. Each message SHALL be a JSON object with parameter medians and metadata (`_spread`, `_agreement`, `_models_used`).

#### Scenario: MQTT topics for hourly consensus
- **WHEN** the enrichment produces consensus for h0 through h48
- **THEN** 49 retained MQTT messages SHALL be published, one per hour

#### Scenario: MQTT Discovery registers hourly consensus device
- **WHEN** MQTT Discovery is triggered for a location with hourly consensus enabled
- **THEN** a device `njord_{location}_hourly_consensus` SHALL be registered with sensor components for each parameter at each hour
