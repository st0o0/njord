# consensus-snapshot Specification

## Purpose

ConsensusSnapshot is the core domain type produced by the consensus pipeline stage. It wraps hourly and daily consensus facets computed from a ModelSnapshot via a pure `Compute` factory method, and serves as the input for all downstream enrichments.

## Requirements

### Requirement: ConsensusSnapshot is the core domain type produced by the consensus pipeline stage

The system SHALL produce a `ConsensusSnapshot` record from a `ModelSnapshot` via a pure `Compute` factory method. `ConsensusSnapshot` SHALL contain a `Location` string, an `HourlyConsensus` facet, and a `DailyConsensus` facet.

#### Scenario: Compute produces a ConsensusSnapshot with hourly and daily facets
- **WHEN** `ConsensusSnapshot.Compute` is called with a `ModelSnapshot` containing 3 models with hourly and daily data for location "Lucerne"
- **THEN** the result SHALL have `Location` equal to "Lucerne", a non-empty `Hourly.Parameters` list, and a non-empty `Daily.Parameters` list

#### Scenario: Empty snapshot for location with no model data
- **WHEN** `ConsensusSnapshot.Compute` is called for a location with no model entries in the snapshot
- **THEN** the result SHALL have empty `Hourly.Parameters` and empty `Daily.Parameters`

### Requirement: HourlyConsensus wraps hourly parameter consensus with cutoff metadata

`HourlyConsensus` SHALL be a record containing `Parameters` (list of `ParameterConsensus`) and `CutoffHour` (int). `CutoffHour` SHALL be the second-to-last model's maximum hour — the last hour where at least 2 models have data.

#### Scenario: CutoffHour from 3 models with different horizons
- **WHEN** 3 models have hourly data extending to h48, h72, and h120
- **THEN** `CutoffHour` SHALL be 72 (second-to-last)

#### Scenario: Fewer than 2 models yields CutoffHour of -1
- **WHEN** only 1 model has hourly data
- **THEN** `CutoffHour` SHALL be -1 and `Parameters` SHALL be empty

### Requirement: DailyConsensus wraps daily parameter consensus with cutoff metadata

`DailyConsensus` SHALL be a record containing `Parameters` (list of `ParameterConsensus`) and `CutoffDay` (int). `CutoffDay` SHALL be the second-to-last model's day count.

#### Scenario: CutoffDay from 3 models with different daily ranges
- **WHEN** 3 models have daily data for 3, 5, and 7 days
- **THEN** `CutoffDay` SHALL be 5 (second-to-last)

#### Scenario: Fewer than 2 models with daily data yields CutoffDay of 0
- **WHEN** only 1 model has daily data
- **THEN** `CutoffDay` SHALL be 0 and `Parameters` SHALL be empty

### Requirement: ConsensusSnapshot.Compute filters horizons with fewer than 2 contributing models

Horizons where fewer than 2 models have a non-null value for any parameter SHALL be excluded from the result. This filtering is integral to `Compute`, not a post-processing step.

#### Scenario: Horizon with 1 model excluded
- **WHEN** hour h70 has data from only 1 model
- **THEN** h70 SHALL NOT appear in `Hourly.Parameters[*].ByHorizon`

#### Scenario: Horizon with 2+ models included
- **WHEN** hour h6 has data from 4 models
- **THEN** h6 SHALL appear in `Hourly.Parameters[*].ByHorizon`

### Requirement: ConsensusSnapshot is the input for the consensus stream stage

The consensus stream stage SHALL be an Akka.Streams `Select` (map) transformation that converts `ModelSnapshot` to a list of `ConsensusSnapshot` (one per configured location).

#### Scenario: Stream stage emits one ConsensusSnapshot per location
- **WHEN** a `ModelSnapshot` arrives with data for locations "Lucerne" and "Zurich"
- **THEN** the consensus stage SHALL emit two `ConsensusSnapshot` instances, one per location

#### Scenario: Pure transformation with no side effects
- **WHEN** the consensus stage processes a `ModelSnapshot`
- **THEN** it SHALL not perform I/O, send actor messages, or modify state

### Requirement: ConsensusSnapshot reuses existing statistical computation

`ConsensusSnapshot.Compute` SHALL delegate to `ConsensusComputer` for all statistical calculations (median, trimmed mean, spread, IQR, agreement, outlier, confidence interval). The `HorizonConsensus` and `ParameterConsensus` records SHALL be unchanged.

#### Scenario: Statistical functions are identical
- **WHEN** comparing `ConsensusSnapshot.Compute` output to the former `ConsensusResult.Compute` output for the same input
- **THEN** `HorizonConsensus` values SHALL be identical for all shared horizons
