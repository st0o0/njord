# hourly-consensus Specification

## Purpose

Unified consensus enrichment that computes per-hour consensus across all weather models with sufficient coverage. Replaces any prior split between horizon-based and hourly consensus with a single `ConsensusEnrichment` (TypeName `"consensus"`).

## Requirements

### Requirement: HourlyConsensusEnrichment computes consensus for every hour with sufficient model coverage
`ConsensusEnrichment` SHALL implement `IStatelessEnrichment` with `TypeName = "consensus"`. On each `ModelSnapshot`, it SHALL compute `ConsensusResult` for hourly horizons from h0 up to the last hour where at least 2 models have forecast data. Hours where fewer than 2 models contribute data for a given parameter SHALL be excluded from the output. The enrichment SHALL be the sole consensus enrichment — there is no separate horizon-based consensus.

#### Scenario: Hourly consensus across models with different horizons
- **WHEN** a `ModelSnapshot` contains icon_d2 (48h coverage) and ecmwf_ifs025 (240h coverage) and gfs_seamless (384h coverage)
- **THEN** the enrichment SHALL produce consensus for h0 through h48 (the last hour where >=2 models have data)
- **AND** hours like h1, h2, h4, h5 (where only hourly-resolution models contribute) SHALL still be included if >=2 hourly models are available

#### Scenario: Single model remaining stops consensus
- **WHEN** only one model has data beyond h48
- **THEN** the enrichment SHALL NOT produce consensus entries for h49 and beyond

#### Scenario: 3-hourly models contribute at their native hours
- **WHEN** ecmwf_ifs025 provides data at h0, h3, h6, h9... (3-hourly)
- **THEN** it SHALL be included in consensus at h3, h6, h9... via the existing +-30min tolerance window
- **AND** it SHALL NOT contribute to h1, h2, h4, h5... (no data within tolerance)

### Requirement: HourlyConsensusEnrichment is independently toggleable
The enrichment SHALL be controlled by `EnrichmentOptions.Consensus.Enabled` (default `true`). There is no separate `HourlyConsensusOptions` — the single `ConsensusOptions` controls the only consensus enrichment.

#### Scenario: Enabled by default
- **WHEN** no configuration override is provided
- **THEN** the enrichment SHALL be enabled and produce hourly consensus output

#### Scenario: Disabled stops output
- **WHEN** `Consensus.Enabled = false`
- **THEN** the enrichment SHALL produce no output

### Requirement: gRPC output uses existing ConsensusUpdate proto message
The consensus SHALL be mapped to a `ConsensusUpdate` proto message and exposed via the `consensus` field in `GetEnrichmentsResponse` (field 8) and `EnrichmentEvent` (field 16). There is no separate `hourly_consensus` field.

#### Scenario: GetEnrichments returns consensus
- **WHEN** a gRPC client calls `GetEnrichments` for a location with consensus enabled
- **THEN** the response SHALL contain a `consensus` field with hourly `HorizonConsensus` entries

#### Scenario: StreamEnrichments emits consensus events
- **WHEN** consensus is computed after a poll cycle
- **THEN** an `EnrichmentEvent` with `type_name = "consensus"` and `consensus` payload SHALL be emitted

### Requirement: MQTT output publishes one topic per hour
The enrichment SHALL publish retained MQTT state messages following the pattern `{baseTopic}/{location}/consensus/h{N}` for each valid hour. Each message SHALL be a JSON object with parameter medians and metadata.

#### Scenario: MQTT topics for consensus
- **WHEN** the enrichment produces consensus for h0 through h48
- **THEN** 49 retained MQTT messages SHALL be published, one per hour

#### Scenario: MQTT Discovery registers consensus device
- **WHEN** MQTT Discovery is triggered for a location with consensus enabled
- **THEN** a device `njord_{location}_consensus` SHALL be registered with sensor components for each parameter at each hour
