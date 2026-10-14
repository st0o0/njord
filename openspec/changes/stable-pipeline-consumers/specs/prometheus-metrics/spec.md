## ADDED Requirements

### Requirement: Stream re-attach metrics
Every re-attach of a stream connection after it was lost SHALL be counted:
- `njord_stream_consumer_rewires_total` (Counter, `Njord.Core` meter): incremented once per lost subscription of a `StreamConsumerActor` subclass - its graph completed or failed, or the pipeline incarnation it was attached to terminated - with label `consumer` (the consumer's name, e.g. `MqttStatePublisher`, `ForecastReadModel`).
- `njord_openmeteo_pipeline_reattaches_total` (Counter, `Njord.OpenMeteo` meter): incremented when `OpenMeteoGateway` re-attaches its forecast branch to the pipeline input after the branch ended or the pipeline terminated. The first attach after the gateway started SHALL NOT be counted.

There SHALL be no `njord_pipeline_stream_rewires_total`: the pipeline does not re-attach. The first attach of a consumer, a failed attach attempt, and the end of a subscription caused by the consumer's own stop SHALL NOT be counted.

#### Scenario: Consumer graph loss is counted
- **WHEN** the graph of `MqttStatePublisher` fails and it re-attaches
- **THEN** `njord_stream_consumer_rewires_total{consumer="MqttStatePublisher"}` is incremented by 1

#### Scenario: Pipeline termination is counted once per consumer
- **WHEN** `ForecastPipeline` terminates while `ForecastReadModel` is attached, and both the termination and the end of the SourceRef reach the read model
- **THEN** `njord_stream_consumer_rewires_total{consumer="ForecastReadModel"}` is incremented by exactly 1

#### Scenario: Pipeline rewire is counted
- **WHEN** the pipeline the gateway is attached to terminates and the gateway re-attaches to its successor
- **THEN** `njord_openmeteo_pipeline_reattaches_total` is incremented by 1

## REMOVED Requirements

### Requirement: Stream rewire metrics
**Reason**: A consumer now learns of a pipeline restart from either its termination or its SourceRef ending, whichever arrives first; excluding one of them would make the counter depend on that race. Replaced by "Stream re-attach metrics", which counts every lost subscription once.
**Migration**: Same metric names; dashboards see one increment per consumer on a pipeline restart.
