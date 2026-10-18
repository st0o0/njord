# health-monitor Specification

## Purpose

Atomic, immutable health state records for ASP.NET Core health checks — each domain publishes its health slice as one atomic reference write.

## Requirements

### Requirement: Health monitor uses atomic immutable records
`NjordHealthMonitor` SHALL expose health state through immutable
records (`MqttHealth`, `PollHealth`, `BudgetHealth`).
Each domain SHALL publish its complete health slice as one atomic reference
write. Readers SHALL receive a consistent record with no torn reads.

#### Scenario: MQTT health is atomic
- **GIVEN** the MQTT connection transitions from connected to disconnected
- **WHEN** a health check reads the MQTT health
- **THEN** `IsConnected` and `DisconnectedSince` are always consistent (never connected=true with a non-null disconnectedSince)

#### Scenario: Poll health reflects last successful poll
- **GIVEN** a successful poll completes at timestamp T
- **WHEN** a health check reads the poll health
- **THEN** `LastSuccessfulPollUtc` equals T

#### Scenario: Budget health carries usage and limits
- **GIVEN** the budget tracker updates daily usage to 50
- **WHEN** a health check reads the budget health
- **THEN** `UsedDaily` equals 50 and limits reflect the configured values
