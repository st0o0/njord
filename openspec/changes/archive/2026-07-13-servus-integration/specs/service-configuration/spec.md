## MODIFIED Requirements

### Requirement: The host is a WebApplication
The service SHALL use `WebApplication.CreateBuilder` as its host builder,
providing Kestrel and the ASP.NET middleware pipeline. DI registrations and
Akka.NET actor system configuration SHALL be delegated to Servus
`IServiceSetupContainer` implementations called from `Program.cs`. The
health-check endpoint at `/healthz` SHALL remain inline.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at
  `/healthz`

#### Scenario: Actor registration uses WithResolvableActors
- **WHEN** the service starts
- **THEN** `MqttEgressActor`, `PipelineActor`, `SchedulerActor`, and
  `EnrichmentActor` are registered in the Akka actor system via
  `WithResolvableActors`

### Requirement: Actors resolve peers via typed extensions
Actors that need references to other actors SHALL use
`Context.GetActor<T>()` from Servus.Akka instead of injecting and
querying `ActorRegistry` directly. The `ActorRegistry` constructor
parameter SHALL be removed from all actors.

#### Scenario: PipelineActor resolves SchedulerActor
- **WHEN** `PipelineActor` needs the scheduler actor reference
- **THEN** it calls `Context.GetActor<SchedulerActor>()`

#### Scenario: MqttEgressActor resolves PipelineActor
- **WHEN** `MqttEgressActor` needs the pipeline actor reference
- **THEN** it calls `Context.GetActor<PipelineActor>()`

#### Scenario: EnrichmentActor resolves peers
- **WHEN** `EnrichmentActor` needs pipeline and egress actor references
- **THEN** it calls `Context.GetActor<PipelineActor>()` and
  `Context.GetActor<MqttEgressActor>()`

### Requirement: Child actors use DI-aware creation
Child actors created by other actors SHALL use
`Context.ResolveChildActor<T>(name, args)` from Servus.Akka instead of
`Props.Create(() => new T(...))`, so that DI services are available to the
child actor.

#### Scenario: ForecastHistoryActor created via ResolveChildActor
- **WHEN** `EnrichmentActor` creates a `ForecastHistoryActor` for a location
- **THEN** it uses `Context.ResolveChildActor<ForecastHistoryActor>(name, location)`
  and the actor receives DI-resolved services plus the location argument
