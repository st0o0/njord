## ADDED Requirements

### Requirement: ConsensusSnapshot records the computation wall-clock time

`ConsensusSnapshot` SHALL include a `ComputedAt` property of type `DateTimeOffset`, set to `timeProvider.GetUtcNow()` inside `ConsensusSnapshot.Compute`. This value represents the wall-clock time at which the consensus was computed and serves as the anchor for resolving offset-based horizon keys to absolute timestamps.

#### Scenario: ComputedAt matches the TimeProvider value at computation time
- **WHEN** `ConsensusSnapshot.Compute` is called with a `FakeTimeProvider` returning `2026-07-31T06:00:00Z`
- **THEN** the resulting `ConsensusSnapshot.ComputedAt` SHALL be `2026-07-31T06:00:00Z`

#### Scenario: ComputedAt is propagated to ConsensusResult
- **WHEN** `EnrichmentActor.ComputeAll` creates a `ConsensusResult` from a `ConsensusSnapshot`
- **THEN** `ConsensusResult.ComputedAt` SHALL equal `ConsensusSnapshot.ComputedAt`
