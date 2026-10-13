## Purpose

Defines which configuration applies while njord runs and which needs a restart, and makes
sure every running component sees the same configuration after a change, instead of the
current mix of live and startup-captured values.

## ADDED Requirements

### Requirement: Settings are classified as hot or restart-required
Analysis parameters (alert thresholds, index preferences, consensus parameters, trend and
derived parameters), the poll interval and the request budget SHALL be hot: a valid change
SHALL take effect without a restart. Locations, models, horizons, forecast days, the
requested parameter set, feature `Enabled` toggles, MQTT, gRPC, persistence and sensor
settings SHALL be restart-required: a change SHALL be persisted and SHALL take effect only
after the next start.

#### Scenario: Alert threshold applies on the next cycle
- **WHEN** the frost thresholds are changed at runtime and the next poll cycle completes
- **THEN** the published frost alert is evaluated against the new thresholds

#### Scenario: Polling cadence applies without restart
- **WHEN** the discovery interval is changed at runtime
- **THEN** pending discovery-phase polls are re-planned with the new interval, without a
  restart and without a double poll for any target

#### Scenario: Poll interval reaches budget projection without restart
- **WHEN** the poll interval is changed at runtime from 60 to 30 minutes
- **THEN** the budget projection and the health check use the 30-minute interval

#### Scenario: Location change waits for restart
- **WHEN** the locations are changed at runtime
- **THEN** the change is persisted, polling continues for the previous locations, and the
  new locations are polled after the next start

### Requirement: Restart-required changes are reported
When a runtime configuration update touches a restart-required setting, the response SHALL
report the update as applied (persisted) and SHALL include a warning that it takes effect
after a restart.

#### Scenario: Admin sets locations
- **WHEN** a client calls the admin `SetLocations` RPC with a valid location list
- **THEN** the response has `applied = true` and its `warnings` contain a restart notice

#### Scenario: Admin sets alert thresholds
- **WHEN** a client calls the admin `SetEnrichment` RPC changing only alert thresholds
- **THEN** the response has `applied = true` and its `warnings` contain no restart notice

### Requirement: Invalid runtime updates are rejected before persisting
A runtime configuration update SHALL be validated with the same rules as startup
validation for the affected section. An invalid update SHALL NOT be persisted and SHALL NOT
reach any running component.

#### Scenario: Invalid threshold order
- **WHEN** a runtime update sets frost thresholds that are not monotonic
- **THEN** the response has `applied = false` with a rejection reason, the override file is
  unchanged, and alerts keep using the previous thresholds

#### Scenario: Poll interval over budget
- **WHEN** a runtime update sets a poll interval whose projected monthly usage exceeds 80 %
  of the effective monthly budget
- **THEN** the update is rejected and the previous polling settings stay in effect

### Requirement: One component observes configuration changes
Exactly one component SHALL subscribe to configuration change notifications. It SHALL
deliver hot changes to the running actors as typed messages. Actors SHALL NOT subscribe to
configuration change notifications themselves.

#### Scenario: Consistent view after a change
- **WHEN** the request budget is changed at runtime
- **THEN** the budget gate, the budget tracker query and the admin `GetConfig` response all
  report the new budget
