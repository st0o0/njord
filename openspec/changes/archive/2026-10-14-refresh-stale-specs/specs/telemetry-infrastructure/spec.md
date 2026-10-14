## MODIFIED Requirements

### Requirement: Akka internal logs flow through Serilog
The Akka.NET actor system SHALL be configured with `ClearLoggers()` and `AddLoggerFactory()` (Akka.Hosting) so that actor logs and internal Akka events (dead letters, supervision failures, persistence errors) are routed through `Microsoft.Extensions.Logging` into the Serilog pipeline.

#### Scenario: Dead letter is visible in logs
- **WHEN** a message is delivered to a dead letter
- **THEN** a log entry for the dead letter appears in the Serilog console sink

## REMOVED Requirements

### Requirement: ServiceDefaults project centralises wiring
**Reason**: There is no `Njord.ServiceDefaults` project and no Aspire AppHost; Serilog is configured in `Program.cs` and health checks in `CoreSetupContainer`/`NjordApplicationSetup`.
**Migration**: See `service-configuration` ("The host is a WebApplication composed of setup containers").
