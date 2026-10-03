## MODIFIED Requirements

### Requirement: SnapshotConsumerActor routes events from BroadcastHub to snapshot actors
`SnapshotConsumerActor` SHALL subscribe to the EgressActor BroadcastHub. It SHALL route `PerModelUpdate` events to `ForecastSnapshotActor` via Ask and wait for Ack. It SHALL route `EnrichmentUpdate` events to `EnrichmentSnapshotActor` via Ask/Ack.

The SnapshotConsumerActor SHALL handle `Terminated` messages by re-requesting a fresh `SourceRef` from the EgressActor and transitioning back to its WaitingForSource state. The actor SHALL NOT silently ignore `Terminated` with a no-op handler.

#### Scenario: Forecast update routed with backpressure
- **WHEN** a PerModelUpdate event arrives from the BroadcastHub
- **THEN** it is sent to ForecastSnapshotActor via Ask and Ack is awaited

#### Scenario: Enrichment update routed with backpressure
- **WHEN** an EnrichmentUpdate event arrives from the BroadcastHub
- **THEN** it is sent to EnrichmentSnapshotActor via Ask and Ack is awaited

#### Scenario: EgressActor restart triggers re-subscription
- **WHEN** SnapshotConsumerActor receives Terminated for the EgressActor
- **THEN** it re-requests a SourceRef from the restarted EgressActor
- **THEN** it transitions to WaitingForSource and rematerializes the stream graph

#### Scenario: SnapshotConsumerActor remains functional after upstream restart
- **WHEN** the EgressActor restarts and SnapshotConsumerActor re-subscribes
- **THEN** new events from the BroadcastHub are routed to snapshot actors
