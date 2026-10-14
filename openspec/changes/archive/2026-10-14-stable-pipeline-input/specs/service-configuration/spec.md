## REMOVED Requirements

### Requirement: Actors resolve peers via marker interfaces
**Reason**: Its example of the pipeline resolving the gateway no longer holds; the dependency direction is now gateway → pipeline.
**Migration**: See "Actors resolve peers through marker interfaces".

## ADDED Requirements

### Requirement: Actors resolve peers through marker interfaces
Actors that need references to other actors SHALL resolve them through the Servus.Akka extensions keyed by the marker interfaces in `Njord.Core/Actors/ActorKeys.cs` — `Context.GetActor<TMarker>()` or `Context.TryGetActor<TMarker>(out ...)` synchronously, or `Context.GetActorAsync<TMarker>()` piped to `Self` — instead of injecting `ActorRegistry` or `IActorRef` constructor parameters. A registry lookup SHALL never be keyed by an actor class. `ForecastPipeline` SHALL resolve no peer: producers and consumers come to it.

#### Scenario: PollScheduler resolves the gateway
- **WHEN** `PollScheduler` is constructed
- **THEN** it calls `Context.GetActor<IForecastGateway>()`

#### Scenario: MqttStatePublisher resolves the pipeline
- **WHEN** `MqttStatePublisher` starts
- **THEN** it calls `Context.GetActor<IForecastPipeline>()`

#### Scenario: The gateway resolves the pipeline asynchronously
- **WHEN** `OpenMeteoGateway` starts or loses its pipeline attachment
- **THEN** it calls `Context.GetActorAsync<IForecastPipeline>()` and pipes the result to itself

#### Scenario: The SensorHub pushes to the pipeline marker
- **WHEN** the SensorHub pushes a snapshot map
- **THEN** it resolves the pipeline through `IForecastPipeline` and skips the push while none is registered
