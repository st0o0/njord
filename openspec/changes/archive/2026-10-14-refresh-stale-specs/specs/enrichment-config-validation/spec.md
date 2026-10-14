## MODIFIED Requirements

### Requirement: ConsensusOptions validates Method and TrimPercent at startup
`ConsensusOptionsValidator` (`IValidateOptions<ConsensusOptions>`) SHALL validate that `ConsensusOptions.Method` is a defined `ConsensusMethod` value (`Mean`, `Median`, `TrimmedMean`), that `TrimPercent` is in the range (0, 0.5) exclusive when Method is `TrimmedMean`, and that `MinQuorum` is at least 1. Invalid values SHALL cause a startup failure with a descriptive error message.

#### Scenario: Valid Method accepted
- **WHEN** ConsensusOptions.Method is `Median`
- **THEN** validation succeeds

#### Scenario: Invalid Method rejected
- **WHEN** ConsensusOptions.Method holds a value that is not a defined `ConsensusMethod`
- **THEN** validation fails with a message listing the valid methods

#### Scenario: TrimPercent validated for TrimmedMean
- **WHEN** ConsensusOptions.Method is `TrimmedMean` and TrimPercent is 0.6
- **THEN** validation fails with a message indicating TrimPercent must be between 0 and 0.5

#### Scenario: TrimPercent ignored for non-TrimmedMean methods
- **WHEN** ConsensusOptions.Method is `Median` and TrimPercent is 0.9
- **THEN** validation succeeds (TrimPercent is irrelevant)

## REMOVED Requirements

### Requirement: HistoryOptions validates SnapshotInterval, RetentionDays, and MinSampleSize at startup
**Reason**: `HistoryOptions` and the history feature no longer exist.
**Migration**: None.
