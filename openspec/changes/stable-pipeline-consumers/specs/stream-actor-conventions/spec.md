## MODIFIED Requirements

### Requirement: Become-phase methods are named for the phase

Actor methods that define a Become phase SHALL be named after the actor's lifecycle phase they represent (e.g., `WaitingForGateway`, `Ready`). Each phase method SHALL register all message handlers for that phase in one place. An actor with a single phase registers its handlers in one method or in the constructor; the stream consumers (`ForecastReadModel`, `MqttStatePublisher`) and their `StreamConsumerActor` base have a single phase.

#### Scenario: ForecastPipeline phases
- **WHEN** inspecting `ForecastPipeline`'s message handling
- **THEN** it has a single operational phase whose handlers (`RequestForecastSink`, `RequestPipelineOutput`, `AnalysisSettingsChanged`, `AnalysisSettingsResult`, `SensorSnapshots`, graph end) are registered in one place, and no waiting phase

#### Scenario: MqttConnection phases
- **WHEN** inspecting `MqttConnection`'s message handling
- **THEN** `Connecting`, `WaitingToReconnect` and `ConnectedState` SHALL each register all handlers of their phase (handlers shared by every phase come from one common method)
