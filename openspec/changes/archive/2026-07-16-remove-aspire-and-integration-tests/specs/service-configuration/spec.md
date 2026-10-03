## MODIFIED Requirements

### Requirement: The host is a WebApplication
The service SHALL use `WebApplication.CreateBuilder` as its host builder,
providing Kestrel and the ASP.NET middleware pipeline. DI registrations and
Akka.NET actor system configuration SHALL be delegated to Servus
`IServiceSetupContainer` implementations called from `Program.cs`. The
health-check endpoint at `/healthz` and liveness endpoint at `/alive` SHALL
be configured in the application setup. Serilog SHALL be configured directly
in `Program.cs`. There SHALL be no dependency on a separate `ServiceDefaults`
project.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at
  `/healthz` and the liveness endpoint at `/alive`

#### Scenario: Actor registration uses WithResolvableActors
- **WHEN** the service starts
- **THEN** `PipelineActor`, `SchedulerActor`, and
  `EnrichmentActor` are registered in the Akka actor system via
  `WithResolvableActors`, plus MQTT actors when `Mqtt.Enabled` is true

#### Scenario: Serilog is configured without ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog is configured directly in `Program.cs` without referencing
  a `ServiceDefaults` project
