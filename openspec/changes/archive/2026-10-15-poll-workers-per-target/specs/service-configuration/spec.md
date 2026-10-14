## MODIFIED Requirements

### Requirement: The host is a WebApplication composed of setup containers
The service SHALL use `WebApplication.CreateBuilder` as its host builder, providing Kestrel and the ASP.NET middleware pipeline. DI registrations and Akka.NET actor system configuration SHALL be delegated to Servus setup containers chained in `Program.cs`. The health-check endpoint at `/healthz`, the liveness endpoint at `/alive`, the Prometheus metrics endpoint and the gRPC services SHALL be mapped in `NjordApplicationSetup`. Serilog SHALL be configured directly in `Program.cs`. There SHALL be no dependency on a separate `ServiceDefaults` project.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at `/healthz` and the liveness endpoint at `/alive`

#### Scenario: Actors are registered through setup containers
- **WHEN** the service starts
- **THEN** `PollManager`, the poll shard region of `PollWorker`s, `ForecastPipeline`, `ForecastReadModel`, `OpenMeteoGateway`, `BudgetTracker`, `SensorHub` and `ConfigManager` are created through the setup containers' `IActorRegistration`s, plus the MQTT actors when `Njord:Mqtt:Enabled` is true

#### Scenario: Serilog is configured without ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog is configured directly in `Program.cs` without referencing a `ServiceDefaults` project

### Requirement: Actors resolve peers through marker interfaces
Actors that need references to other actors SHALL resolve them through the Servus.Akka extensions keyed by the marker interfaces in `Njord.Core/Actors/ActorKeys.cs` — `Context.GetActor<TMarker>()` or `Context.TryGetActor<TMarker>(out ...)` synchronously, or `Context.GetActorAsync<TMarker>()` piped to `Self` — instead of injecting `ActorRegistry` or `IActorRef` constructor parameters. A registry lookup SHALL never be keyed by an actor class. `ForecastPipeline` SHALL resolve no peer: producers and consumers come to it.

#### Scenario: PollScheduler resolves the gateway
- **WHEN** a `PollWorker` polls its target
- **THEN** it calls `Context.TryGetActor<IForecastGateway>(out ...)` for that poll, and the `PollManager` resolves the poll shard region with `Context.TryGetActor<IPollRegion>(out ...)`

#### Scenario: MqttStatePublisher resolves the pipeline
- **WHEN** `MqttStatePublisher` starts
- **THEN** it calls `Context.GetActor<IForecastPipeline>()`

#### Scenario: The gateway resolves the pipeline asynchronously
- **WHEN** `OpenMeteoGateway` starts or loses its pipeline attachment
- **THEN** it calls `Context.GetActorAsync<IForecastPipeline>()` and pipes the result to itself

#### Scenario: The SensorHub pushes to the pipeline marker
- **WHEN** the SensorHub pushes a snapshot map
- **THEN** it resolves the pipeline through `IForecastPipeline` and skips the push while none is registered
