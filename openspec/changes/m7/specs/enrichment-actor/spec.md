## ADDED Requirements

### Requirement: The EnrichmentActor materializes a history consumer stream when enabled
The `EnrichmentActor` SHALL materialize a history consumer stream when `EnrichmentOptions.History.Enabled` is `true`. The stream SHALL subscribe to the `BroadcastHub<ModelSnapshot>`, forward each snapshot to the `ForecastHistoryActor` for persistence, query the actor for history state, compute analysis via `HistoryAnalyzer`, serialize results to `MqttMessage`s, apply delta publishing, and sink into the MqttSinkRef. The `ForecastHistoryActor` SHALL be created per location as a child of the `EnrichmentActor`. If `History.Enabled` is `false`, no history consumer stream or history actors SHALL be materialized.

#### Scenario: History consumer enabled
- **WHEN** `History.Enabled` is `true`
- **THEN** the history consumer stream and per-location ForecastHistoryActors are materialized

#### Scenario: History consumer disabled
- **WHEN** `History.Enabled` is `false`
- **THEN** no history consumer stream or history actors are materialized

#### Scenario: History actors are children of EnrichmentActor
- **WHEN** the history consumer is enabled with 2 locations
- **THEN** 2 ForecastHistoryActor children exist, one per location
