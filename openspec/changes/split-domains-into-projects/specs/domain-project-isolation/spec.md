## ADDED Requirements

### Requirement: Each domain is a self-contained project
Each domain (Weather Alerts, AirQuality, Flood) SHALL be its own project with its own alert rules, projectors, enrichments, DI registration, and test project. Domain projects SHALL reference only Njord.Core and below, never each other.

#### Scenario: AirQuality project compiles independently
- **WHEN** Njord.AirQuality is built in isolation (with its dependencies)
- **THEN** it SHALL compile with 0 errors and contain all AQ-specific code

#### Scenario: No cross-domain references
- **WHEN** the project references are inspected
- **THEN** no domain project SHALL reference another domain project

### Requirement: Domain-specific MQTT payloads live in domain projects
Domain-specific payload records SHALL live in the domain project that produces them, not in Njord.Mqtt.

#### Scenario: AirQualityPayload in Njord.AirQuality
- **WHEN** the AirQualityPayload type is located
- **THEN** it SHALL be in the Njord.AirQuality project, not Njord.Mqtt
