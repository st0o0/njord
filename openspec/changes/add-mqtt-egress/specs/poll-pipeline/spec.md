# poll-pipeline Specification (Delta)

## ADDED Requirements

### Requirement: Cycle results feed the MQTT egress
The pipeline SHALL hand every cycle result to the MQTT egress for telemetry
publishing and SHALL log one summary line per cycle (cycle id, per-model
success/failure, received vs. missing counts). Egress unavailability (broker
down, actor restarting) MUST NOT fail or stall the poll pipeline.

#### Scenario: Cycle result reaches egress and log
- **WHEN** a cycle result is emitted
- **THEN** it is delivered to the egress exactly once and exactly one
  summary log entry for that cycle id is written

#### Scenario: Broker outage does not stall polling
- **WHEN** the MQTT broker is unreachable during a cycle
- **THEN** the poll pipeline continues its cadence and later cycles are
  published once the connection returns

## REMOVED Requirements

### Requirement: v1 sink logs cycle summaries
**Reason**: The log-only sink was explicitly temporary ("until consensus and
MQTT egress exist"); egress now exists.
**Migration**: Covered by "Cycle results feed the MQTT egress" — the summary
log line survives as part of that requirement.
