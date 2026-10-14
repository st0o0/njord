## ADDED Requirements

### Requirement: Availability is published directly on connect and stop
`MqttConnection` SHALL publish a retained `online` on `{BaseTopic}/status` after every successful connect and a retained `offline` when it stops, by calling `IMqttTransport.SendAsync` directly. No queue or MergeHub SHALL be involved. The broker-side Last Will (`offline`, retained) SHALL cover connection loss.

#### Scenario: Online on connect
- **WHEN** the broker connection succeeds
- **THEN** `online` is published retained on the availability topic

#### Scenario: Offline on shutdown
- **WHEN** the actor stops
- **THEN** `offline` is published retained on the availability topic

## REMOVED Requirements

### Requirement: ModelStateActor owns a BroadcastHub and serves SourceRefs to consumers
**Reason**: `ModelStateActor`, `EgressEvent` and `Njord.Egress` no longer exist.
**Migration**: `ForecastPipeline` owns the `BroadcastHub<PipelineEvent>`; see `pipeline-composition`.

### Requirement: The Publish Sink buffers during disconnect with DropHead overflow
**Reason**: There is no publish sink and no outage buffer. `MqttStatePublisher` calls `IMqttTransport.SendAsync` directly from a `SelectAsync(1)` stage and `MqttNetPublisher` publishes straight through MQTTnet.
**Migration**: None.

### Requirement: Availability messages are fed via Source.Queue
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Availability is published directly on connect and stop" in this capability.

### Requirement: GrpcSnapshotConsumerActor routes events from producer BroadcastHubs to snapshot actors
**Reason**: `GrpcSnapshotConsumerActor` and the snapshot actors no longer exist.
**Migration**: `ForecastReadModel` consumes the pipeline output; see `consumer-model`.
