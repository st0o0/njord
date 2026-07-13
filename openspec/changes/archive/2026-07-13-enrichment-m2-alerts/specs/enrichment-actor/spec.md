# enrichment-actor Specification (Delta)

## Purpose

Extends the EnrichmentActor with the alert consumer stream as an additional BroadcastHub subscriber.

## ADDED Requirements

### Requirement: The EnrichmentActor materializes an alert consumer stream when enabled
The `EnrichmentActor` SHALL materialize an alert consumer stream when `EnrichmentOptions.Alerts.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, evaluate all alert types via `AlertEvaluator`, serialize results to `MqttMessage`s, apply delta publishing, and sink into the MqttSinkRef. If `Alerts.Enabled` is `false`, no alert consumer stream SHALL be materialized.

#### Scenario: Alert consumer alongside consensus
- **WHEN** both `Consensus.Enabled` and `Alerts.Enabled` are `true`
- **THEN** two consumer streams subscribe to the BroadcastHub independently

#### Scenario: Alert consumer only
- **WHEN** `Consensus.Enabled` is `false` and `Alerts.Enabled` is `true`
- **THEN** only the alert consumer stream subscribes to the BroadcastHub

#### Scenario: Alert consumer disabled
- **WHEN** `Alerts.Enabled` is `false`
- **THEN** no alert consumer stream is materialized
