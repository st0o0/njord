## ADDED Requirements

### Requirement: The EnrichmentActor materializes an index consumer stream when enabled
The `EnrichmentActor` SHALL materialize an index consumer stream when `EnrichmentOptions.Indices.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, compute all indices via `IndexResult.Compute`, serialize results to `MqttMessage`s, apply delta publishing, and sink into the MqttSinkRef. If `Indices.Enabled` is `false`, no index consumer stream SHALL be materialized.

#### Scenario: Index consumer enabled
- **WHEN** `Indices.Enabled` is `true`
- **THEN** the index consumer stream subscribes to the BroadcastHub

#### Scenario: Index consumer disabled
- **WHEN** `Indices.Enabled` is `false`
- **THEN** no index consumer stream is materialized
