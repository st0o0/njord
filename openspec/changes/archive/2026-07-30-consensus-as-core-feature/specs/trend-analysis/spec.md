## MODIFIED Requirements

### Requirement: Trend direction compares consensus median between snapshots

Trend analysis SHALL accept `ConsensusSnapshot` and `ConsensusSnapshot?` previous instead of `ModelSnapshot`. Trend direction compares consensus medians at the same horizon between two consecutive snapshots.

#### Scenario: Rising trend
- **WHEN** current consensus median temperature at h3 is 22°C and previous was 19°C
- **THEN** the trend direction is "rising" with delta 3.0

#### Scenario: Falling trend
- **WHEN** current consensus median temperature at h3 is 15°C and previous was 19°C
- **THEN** the trend direction is "falling" with delta -4.0

#### Scenario: Stable within dead-band
- **WHEN** the delta between current and previous consensus median is within the dead-band
- **THEN** the trend direction is "stable"

#### Scenario: Null previous
- **WHEN** `previous` is null
- **THEN** no trend events are produced

#### Scenario: Null current
- **WHEN** the current consensus median is null at a horizon
- **THEN** that horizon's trend is null

### Requirement: Consensus stability compares IQR between snapshots

Stability SHALL compare IQR values from `ConsensusSnapshot.Hourly` between current and previous.

#### Scenario: Converging models
- **WHEN** current IQR is smaller than previous IQR
- **THEN** stability label is "converging"

#### Scenario: Diverging models
- **WHEN** current IQR is larger than previous IQR
- **THEN** stability label is "diverging"

#### Scenario: Stable
- **WHEN** IQR ratio is within tolerance
- **THEN** stability label is "stable"

### Requirement: Predictability decay measures spread growth across horizons

Decay SHALL use spread values from `ConsensusSnapshot.Hourly.Parameters`.

#### Scenario: Gradual decay
- **WHEN** spread increases from h0 to h24
- **THEN** a positive decay rate is computed

#### Scenario: Flat spread
- **WHEN** spread is constant across horizons
- **THEN** decay rate is near zero

#### Scenario: Insufficient data
- **WHEN** fewer than 2 horizons have spread values
- **THEN** decay is null

### Requirement: TrendResult aggregates all trend analysis and serializes to MQTT

`TrendResult` SHALL be computed from `ConsensusSnapshot` pairs. Location comes from `ConsensusSnapshot.Location`.

#### Scenario: Trend message content
- **WHEN** trends are serialized to MQTT
- **THEN** one retained message is published with direction, timing, stability, and decay

#### Scenario: No previous snapshot
- **WHEN** previous `ConsensusSnapshot` is null
- **THEN** no trend message is emitted

#### Scenario: Retained message
- **WHEN** a trend message is published
- **THEN** it is retained
