# enrichment-config-validation Specification

## Purpose

Startup validation for enrichment configuration options (ConsensusOptions, EnergyOptions, HistoryOptions) using `IValidateOptions<T>`.

## Requirements

### Requirement: ConsensusOptions validates Method and TrimPercent at startup
The system SHALL validate `ConsensusOptions.Method` is one of "Mean", "Median", or "TrimmedMean". When Method is "TrimmedMean", `TrimPercent` SHALL be in the range (0, 0.5) exclusive. Invalid values SHALL cause a startup failure with a descriptive error message via `IValidateOptions<ConsensusOptions>`.

#### Scenario: Valid Method accepted
- **WHEN** ConsensusOptions.Method is "Median"
- **THEN** validation succeeds

#### Scenario: Invalid Method rejected
- **WHEN** ConsensusOptions.Method is "InvalidMethod"
- **THEN** validation fails with a message listing the valid methods

#### Scenario: TrimPercent validated for TrimmedMean
- **WHEN** ConsensusOptions.Method is "TrimmedMean" and TrimPercent is 0.6
- **THEN** validation fails with a message indicating TrimPercent must be between 0 and 0.5

#### Scenario: TrimPercent ignored for non-TrimmedMean methods
- **WHEN** ConsensusOptions.Method is "Median" and TrimPercent is 0.9
- **THEN** validation succeeds (TrimPercent is irrelevant)

### Requirement: EnergyOptions validates CarnotEfficiency and FlowTemp at startup
The system SHALL validate `EnergyOptions.CarnotEfficiency` is in the range (0, 1) exclusive and `FlowTemp` is greater than 0. Invalid values SHALL cause a startup failure with a descriptive error message via `IValidateOptions<EnergyOptions>`.

#### Scenario: Valid CarnotEfficiency accepted
- **WHEN** EnergyOptions.CarnotEfficiency is 0.45
- **THEN** validation succeeds

#### Scenario: CarnotEfficiency out of range rejected
- **WHEN** EnergyOptions.CarnotEfficiency is 1.5
- **THEN** validation fails with a message indicating the valid range

#### Scenario: FlowTemp must be positive
- **WHEN** EnergyOptions.FlowTemp is -10
- **THEN** validation fails with a message indicating FlowTemp must be positive

### Requirement: HistoryOptions validates SnapshotInterval, RetentionDays, and MinSampleSize at startup
The system SHALL validate that `HistoryOptions.SnapshotInterval`, `RetentionDays`, and `MinSampleSize` are all greater than 0. Invalid values SHALL cause a startup failure with a descriptive error message via `IValidateOptions<HistoryOptions>`.

#### Scenario: Valid HistoryOptions accepted
- **WHEN** SnapshotInterval is 100, RetentionDays is 30, MinSampleSize is 48
- **THEN** validation succeeds

#### Scenario: Zero SnapshotInterval rejected
- **WHEN** HistoryOptions.SnapshotInterval is 0
- **THEN** validation fails with a message indicating SnapshotInterval must be positive

#### Scenario: Negative RetentionDays rejected
- **WHEN** HistoryOptions.RetentionDays is -1
- **THEN** validation fails with a message indicating RetentionDays must be positive
