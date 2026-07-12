# service-configuration Specification (delta)

## MODIFIED Requirements

### Requirement: The host is a WebApplication
The service SHALL use `WebApplication.CreateBuilder` as its host builder,
providing Kestrel and the ASP.NET middleware pipeline. The Akka.NET actor
system, options binding, and all existing DI registrations SHALL remain
unchanged.

#### Scenario: Health middleware is registered
- **WHEN** the service starts
- **THEN** the middleware pipeline includes the health-check endpoint at
  `/healthz`

#### Scenario: Existing actor registration is preserved
- **WHEN** the service starts
- **THEN** the `MqttConnectionActor` and `PipelineGuardianActor` are registered
  in the Akka actor system as before
