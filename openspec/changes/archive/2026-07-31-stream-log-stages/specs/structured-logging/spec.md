## ADDED Requirements

### Requirement: Namespace override documentation for stream tracing

The example configuration (`appsettings.Example.json`) SHALL include commented examples showing how to enable Debug-level stream tracing per subsystem using Serilog `MinimumLevel.Override` entries.

#### Scenario: Example config contains stream tracing overrides
- **WHEN** reading `appsettings.Example.json`
- **THEN** it contains commented override entries for stream log stage names (e.g., `pipeline-fetch-in`, `mqtt-send`) with an explanation of their purpose
