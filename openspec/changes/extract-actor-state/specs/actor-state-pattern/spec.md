## MODIFIED Requirements

### Requirement: Actors are thin shells delegating to state records
All actors (GrpcConsumer, MqttConnection, PollWorker) SHALL hold their domain state in a sealed immutable record with extension methods. Actor message handlers SHALL delegate to state Apply/Process methods and only perform actor-specific operations (Tell, Timers, stream materialization).

#### Scenario: GrpcConsumer handler is a thin shell
- **WHEN** GrpcConsumer receives a WeatherDataReceived event
- **THEN** it SHALL call `_state = _state.Apply(event)` and perform no other domain logic inline

#### Scenario: GrpcConsumer handles all alert types
- **WHEN** GrpcConsumer receives an AlertRaised event with AlertType.Pollen
- **THEN** the alert SHALL be stored in the state's alert cache (not silently dropped)

#### Scenario: Pure state tests without Akka
- **WHEN** GrpcConsumerState.Apply is called with test data
- **THEN** the result SHALL be testable as a pure function without TestKit
