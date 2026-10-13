# health-monitor Delta

## ADDED Requirements

### Requirement: Health monitor uses atomic immutable snapshots
`NjordHealthMonitor` SHALL expose health state through immutable snapshot
records (`MqttHealthSnapshot`, `PollHealthSnapshot`, `BudgetHealthSnapshot`).
Each domain SHALL publish its complete health slice as one atomic reference
write. Readers SHALL receive a consistent snapshot with no torn reads.

#### Scenario: MQTT health snapshot is atomic
- **GIVEN** the MQTT connection transitions from connected to disconnected
- **WHEN** a health check reads the MQTT snapshot
- **THEN** `IsConnected` and `DisconnectedSince` are always consistent (never connected=true with a non-null disconnectedSince)

#### Scenario: Poll health snapshot reflects last successful poll
- **GIVEN** a successful poll completes at timestamp T
- **WHEN** a health check reads the poll snapshot
- **THEN** `LastSuccessfulPollUtc` equals T

#### Scenario: Budget snapshot carries usage and limits
- **GIVEN** the budget tracker updates daily usage to 50
- **WHEN** a health check reads the budget snapshot
- **THEN** `UsedDaily` equals 50 and limits reflect the configured values

## REMOVED Requirements

### Requirement: NjordHealthState mutable fields
Removed. `NjordHealthState` with per-field `Interlocked` writes is replaced
by `NjordHealthMonitor` with atomic snapshot references.

#### Scenario: removed
- **WHEN** removed
- **THEN** removed
