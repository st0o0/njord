## ADDED Requirements

### Requirement: The EnrichmentActor materializes a trend consumer stream when enabled
The `EnrichmentActor` SHALL materialize a trend consumer stream when `EnrichmentOptions.Trends.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, use a `Scan` operator to carry a `(ModelSnapshot? Previous, ModelSnapshot Current)` pair, compute trends via `TrendResult.Compute` when a previous snapshot exists, serialize results to `MqttMessage`s, apply delta publishing, and sink into the MqttSinkRef. If `Trends.Enabled` is `false`, no trend consumer stream SHALL be materialized. The first snapshot after materialization SHALL produce no trend output (no previous to compare against).

#### Scenario: Trend consumer with scan pairing
- **WHEN** `Trends.Enabled` is `true` and two consecutive snapshots arrive
- **THEN** the trend consumer computes trends comparing the second snapshot to the first

#### Scenario: First snapshot produces no output
- **WHEN** `Trends.Enabled` is `true` and the first snapshot arrives
- **THEN** no trend messages are emitted (no previous snapshot for comparison)

#### Scenario: Trend consumer disabled
- **WHEN** `Trends.Enabled` is `false`
- **THEN** no trend consumer stream is materialized
