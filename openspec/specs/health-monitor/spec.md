# health-monitor Specification

## Purpose

Atomic, immutable health state records for ASP.NET Core health checks — each domain publishes its health slice as one atomic reference write.
## Requirements
### Requirement: Feature health state is owned by the feature and atomic
Each feature that exposes health SHALL own an thread-safe health state type in its own project (`PollHealthState` in Pipeline, `MqttConnectionState` in Mqtt, `BudgetUsageState` in Budget). The owning actor SHALL be the only writer and SHALL publish a complete immutable record per write as one atomic reference swap; the feature's health check SHALL be the only reader. `Njord.Core` SHALL NOT declare health state types.

#### Scenario: MQTT state is atomic
- **WHEN** the MQTT connection transitions from connected to disconnected
- **THEN** a reader never observes `IsConnected = true` together with a non-null disconnect timestamp

#### Scenario: Poll state reflects the last successful poll
- **WHEN** a poll completes at timestamp T
- **THEN** the poll health state reports T

#### Scenario: Core has no health types
- **WHEN** `Njord.Core` is inspected
- **THEN** it declares no `NjordHealthMonitor`, `MqttHealth`, `PollHealth` or `BudgetHealth`

