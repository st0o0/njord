## MODIFIED Requirements

### Requirement: ModelStateActor produces PerModelUpdate events

The `ModelStateActor` (renamed from `MqttPublisherActor`) SHALL reside in `Njord.Egress`, subscribe to the Pipeline BroadcastHub via `ISourceRef<FetchOutcome>`, transform `FetchOutcome.Success` into `EgressEvent.PerModelUpdate`, and send them into the EgressActor's MergeHub via `ISinkRef<EgressEvent>`. It SHALL NOT reference any types from `Njord.Mqtt`. Additionally, after each successful fetch, it SHALL track which parameters the model delivered with non-null values and send a `ModelCapabilityLearned` message to the `DiscoveryActor` when the tracked set changes.

#### Scenario: Successful fetch produces PerModelUpdate
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Success` from the pipeline
- **THEN** it SHALL compute per-horizon payloads via `HorizonProjection.BuildPerHorizon` and emit an `EgressEvent.PerModelUpdate` with the location, model, and horizon data

#### Scenario: Unchanged data is deduplicated
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Success` whose per-horizon payloads are identical to the previously emitted values for the same (location, model, horizon)
- **THEN** it SHALL NOT emit an `EgressEvent.PerModelUpdate`

#### Scenario: Fetch failures are dropped
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Failure`
- **THEN** it SHALL NOT emit any `EgressEvent`

#### Scenario: First successful fetch sends capability message
- **WHEN** `ModelStateActor` processes the first `FetchOutcome.Success` for a (location, model) pair
- **THEN** it SHALL send a `ModelCapabilityLearned` message to the `DiscoveryActor` with the observed supported parameters and applicable horizons
