# vertical-feature-libraries Delta

## MODIFIED Requirements

### Requirement: Feature analyzers take ConsensusSnapshot directly
Each feature analyzer SHALL accept `ConsensusSnapshot` as its primary input
instead of a feature-specific input interface. Per-feature extras:
- `AlertAnalyzer.Compute(ConsensusSnapshot)` — pure
- `DerivedAnalyzer.Compute(ConsensusSnapshot)` — `Horizons` DI-injected
- `TrendAnalyzer.Compute(ConsensusSnapshot, ConsensusSnapshot?)` — previous
- `IndexAnalyzer.Compute(ConsensusSnapshot, SensorSnapshot?)` — sensors

No `IAlertInput`, `IDerivedInput`, `ITrendInput`, `IIndexInput` interfaces.
No `PollContext` god-object. Each analyzer returns its typed result or null
when disabled.

#### Scenario: AlertAnalyzer computes from ConsensusSnapshot
- **WHEN** `AlertAnalyzer.Compute(consensus)` is called with a valid ConsensusSnapshot
- **THEN** it returns `AlertResult` with alerts for the consensus location

#### Scenario: DerivedAnalyzer uses DI-injected horizons
- **WHEN** `DerivedAnalyzer.Compute(consensus)` is called
- **THEN** it uses horizons from its constructor, not from the input

#### Scenario: TrendAnalyzer receives previous in its signature
- **WHEN** `TrendAnalyzer.Compute(consensus, previous)` is called with a non-null previous
- **THEN** it computes trends between the two snapshots

#### Scenario: IndexAnalyzer receives sensors in its signature
- **WHEN** `IndexAnalyzer.Compute(consensus, sensors)` is called
- **THEN** it uses sensor data for indoor temperature overrides

## REMOVED Requirements

### Requirement: Feature libraries define typed input interfaces
Removed. Input interfaces (`IAlertInput`, `IDerivedInput`, `ITrendInput`,
`IIndexInput`) are replaced by direct `ConsensusSnapshot` parameters.
`PollContext` implementing all four is removed.

#### Scenario: removed
- **WHEN** removed
- **THEN** removed
