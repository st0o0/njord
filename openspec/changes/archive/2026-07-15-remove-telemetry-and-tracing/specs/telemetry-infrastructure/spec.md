## REMOVED Requirements

### Requirement: OpenTelemetry tracing is registered
**Reason**: No OTLP collector on target server; tracing infrastructure is dead code.
**Migration**: None — traces were never consumed.

### Requirement: OpenTelemetry metrics are registered
**Reason**: No OTLP collector on target server; metrics infrastructure is dead code.
**Migration**: None — metrics were never consumed.

### Requirement: OTLP export is opt-in
**Reason**: Entire OTel stack removed; opt-in export has no meaning without the SDK.
**Migration**: None — the env var `OTEL_EXPORTER_OTLP_ENDPOINT` is no longer read for OTel purposes. Serilog OTLP sink is also removed.

### Requirement: NjordTelemetry is the single source of instrument definitions
**Reason**: No instruments remain; the class is deleted.
**Migration**: None — no replacement needed.

### Requirement: Resource attributes identify the service
**Reason**: OTel SDK removed; resource attributes are part of the OTel pipeline.
**Migration**: None.

## MODIFIED Requirements

### Requirement: Serilog enrichers provide context
The Serilog pipeline SHALL enrich log entries with machine name and thread id.

#### Scenario: Log entry includes machine context
- **WHEN** a log entry is written
- **THEN** the log entry properties include `MachineName` and `ThreadId`

### Requirement: ServiceDefaults project centralises wiring
A `Njord.ServiceDefaults` project SHALL provide extension methods to configure
Serilog and health checks. The main service project and the Aspire AppHost
SHALL reference this project.

#### Scenario: Service uses ServiceDefaults
- **WHEN** the service starts
- **THEN** Serilog and health checks are configured via `Njord.ServiceDefaults`
  extension methods
