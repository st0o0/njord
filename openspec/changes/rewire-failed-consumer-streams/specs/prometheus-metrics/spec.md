## ADDED Requirements

### Requirement: Stream rewire metrics
Every rebuild of a stream graph after the graph ended on its own SHALL be counted:
- `njord_stream_consumer_rewires_total` (Counter, `Njord.Core` meter): incremented when a `StreamConsumerActor` subclass rewires after its graph completed or failed, with label `consumer` (the consumer's name, e.g. `MqttStatePublisher`, `ForecastReadModel`).
- `njord_pipeline_stream_rewires_total` (Counter, `Njord.Pipeline` meter): incremented when `ForecastPipeline` requests a new forecast stream because its analysis graph completed or failed while the gateway was alive.

A rebuild caused by a dependency actor's termination or by the owning actor's stop SHALL NOT be counted.

#### Scenario: Consumer rewire is counted
- **WHEN** the graph of `MqttStatePublisher` fails and it rewires
- **THEN** `njord_stream_consumer_rewires_total{consumer="MqttStatePublisher"}` is incremented by 1

#### Scenario: Pipeline rewire is counted
- **WHEN** the forecast stream of `ForecastPipeline` fails while the gateway is alive
- **THEN** `njord_pipeline_stream_rewires_total` is incremented by 1

#### Scenario: Dependency loss is not counted as a rewire
- **WHEN** `ForecastPipeline` terminates and `ForecastReadModel` re-resolves it
- **THEN** `njord_stream_consumer_rewires_total{consumer="ForecastReadModel"}` is not incremented
