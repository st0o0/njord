## MODIFIED Requirements

### Requirement: Stream rewire metrics
Every rebuild of a stream connection after it ended on its own SHALL be counted:
- `njord_stream_consumer_rewires_total` (Counter, `Njord.Core` meter): incremented when a `StreamConsumerActor` subclass rewires after its graph completed or failed, with label `consumer` (the consumer's name, e.g. `MqttStatePublisher`, `ForecastReadModel`).
- `njord_openmeteo_pipeline_reattaches_total` (Counter, `Njord.OpenMeteo` meter): incremented when `OpenMeteoGateway` re-attaches its forecast branch to the pipeline input after the branch ended or the pipeline terminated. The first attach after the gateway started SHALL NOT be counted.

There SHALL be no `njord_pipeline_stream_rewires_total`: the pipeline no longer rewires. A rebuild caused by a dependency actor's termination of a consumer, or by the owning actor's stop, SHALL NOT be counted as a consumer rewire.

#### Scenario: Consumer rewire is counted
- **WHEN** the graph of `MqttStatePublisher` fails and it rewires
- **THEN** `njord_stream_consumer_rewires_total{consumer="MqttStatePublisher"}` is incremented by 1

#### Scenario: Pipeline rewire is counted
- **WHEN** the pipeline the gateway is attached to terminates and the gateway re-attaches to its successor
- **THEN** `njord_openmeteo_pipeline_reattaches_total` is incremented by 1

#### Scenario: Dependency loss is not counted as a rewire
- **WHEN** `ForecastPipeline` terminates and `ForecastReadModel` re-resolves it
- **THEN** `njord_stream_consumer_rewires_total{consumer="ForecastReadModel"}` is not incremented
