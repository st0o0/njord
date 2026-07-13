## ADDED Requirements

### Requirement: The EnrichmentActor materializes an energy consumer stream when enabled
The `EnrichmentActor` SHALL materialize an energy consumer stream when `EnrichmentOptions.Energy.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, compute all energy values via `EnergyResult.Compute`, serialize results to `MqttMessage`s, apply delta publishing, and sink into the MqttSinkRef. If `Energy.Enabled` is `false`, no energy consumer stream SHALL be materialized.

#### Scenario: Energy consumer enabled
- **WHEN** `Energy.Enabled` is `true`
- **THEN** the energy consumer stream subscribes to the BroadcastHub

#### Scenario: Energy consumer disabled
- **WHEN** `Energy.Enabled` is `false`
- **THEN** no energy consumer stream is materialized
