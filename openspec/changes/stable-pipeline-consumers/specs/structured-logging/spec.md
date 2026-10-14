## MODIFIED Requirements

### Requirement: SourceRef handshake logs at Debug level
The `StreamConsumerActor` base SHALL log the receipt of a SourceRef for its subclass at Debug level, including the source actor path. Materializing the analysis pipeline in `ForecastPipeline` is a lifecycle transition and SHALL be logged at Information level.

#### Scenario: SourceRef received not visible at Information level
- **WHEN** `MqttStatePublisher` receives `RequestPipelineOutputCompleted` and the minimum log level is Information
- **THEN** no "SourceRef received" message appears in console output

#### Scenario: SourceRef received visible at Debug level
- **WHEN** `MqttStatePublisher` receives `RequestPipelineOutputCompleted` and the minimum log level is Debug
- **THEN** a Debug message `"Pipeline SourceRef received from {Source}"` with the sender's actor path appears
