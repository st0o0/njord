## ADDED Requirements

### Requirement: The EnrichmentActor materializes a derived consumer stream when enabled
The `EnrichmentActor` SHALL materialize a derived consumer stream when `EnrichmentOptions.Derived.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, compute all derived values via `DerivedResult.Compute`, serialize results to `MqttMessage`s, apply delta publishing, and sink into the MqttSinkRef. If `Derived.Enabled` is `false`, no derived consumer stream SHALL be materialized.

#### Scenario: Derived consumer alongside consensus and alerts
- **WHEN** `Consensus.Enabled`, `Alerts.Enabled`, and `Derived.Enabled` are all `true`
- **THEN** three consumer streams subscribe to the BroadcastHub independently

#### Scenario: Derived consumer only
- **WHEN** `Consensus.Enabled` and `Alerts.Enabled` are `false` and `Derived.Enabled` is `true`
- **THEN** only the derived consumer stream subscribes to the BroadcastHub

#### Scenario: Derived consumer disabled
- **WHEN** `Derived.Enabled` is `false`
- **THEN** no derived consumer stream is materialized
