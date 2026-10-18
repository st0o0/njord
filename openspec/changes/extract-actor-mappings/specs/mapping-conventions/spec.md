## ADDED Requirements

### Requirement: Actors and clients contain no mapping code
Actor and service classes SHALL NOT declare methods that only transform one contract into another (named `Map*`, `Parse*Array`, `Build*Uri` or equivalent). Such transformations SHALL be extension methods in the owning project's `Mapping` folder, receiving any settings they need as explicit arguments or as a small context record.

#### Scenario: MQTT publisher delegates event mapping
- **WHEN** `MqttStatePublisher` receives a pipeline event
- **THEN** it calls `PipelineEvent.ToMqttMessages(MqttPublishContext)` and `ToLocationName()` and only dedups and publishes the result

#### Scenario: Event mapping is testable without the actor
- **WHEN** a forecast, consensus, alert, derived, trend or index event is mapped with a context
- **THEN** the mapping spec asserts topic, payload and retain flag without an `ActorSystem`

#### Scenario: Open-Meteo series mapping is independent of the client
- **WHEN** an `OpenMeteoTimeSeries` is mapped to a hourly or daily domain series
- **THEN** the mapping extension is called with the resolved parameter set and no HTTP client, and it returns the same series as before

#### Scenario: Log level conversion is exhaustive
- **WHEN** `ToLogLevel()` is called for each Akka log level
- **THEN** each returns its documented level and an undefined value throws `ArgumentOutOfRangeException`

### Requirement: The architecture spec flags mapping methods in actors and services
`MappingConventionArchitectureSpec` SHALL fail when a class deriving from an actor base class, or a service class registered by a setup container, declares a method named `Map*`, `Parse*Array` or `Build*Uri`.

#### Scenario: A new private mapper in an actor
- **WHEN** an actor declares `private IEnumerable<X> MapToX(...)`
- **THEN** the architecture spec fails
