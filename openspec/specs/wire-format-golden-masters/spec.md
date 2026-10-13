# wire-format-golden-masters Specification

## Purpose
Pins the externally visible output of njord (MQTT state messages and gRPC analysis and
forecast responses) with snapshot tests, so internal refactorings can prove they changed
nothing a consumer sees.
## Requirements
### Requirement: MQTT state messages are pinned by golden masters
For one fixed, deterministic fixture (fixed `TimeProvider`, fixed locations, models,
horizons and forecast values), the system SHALL have a snapshot test per state builder
(consensus, alerts, derived, trends, indices) that records every produced MQTT message
(topic, payload, retain flag) in order. The fixture SHALL exercise every alert type, every
index kind, every trend field, every derived field, hourly and daily horizons, and at least
one `null`/unavailable value.

#### Scenario: Unchanged builder output passes
- **WHEN** the state builders run against the fixture and produce the same messages as the
  approved snapshot
- **THEN** the golden-master tests pass

#### Scenario: Any payload change fails
- **WHEN** a refactoring changes a single topic segment, JSON key, value formatting or
  message order
- **THEN** the corresponding golden-master test fails and shows the diff

### Requirement: gRPC analysis and forecast responses are pinned by golden masters
For the same fixture, the system SHALL have snapshot tests for the proto messages produced
for forecast updates and for every analysis kind (consensus, alerts, derived, trends,
indices), recorded in a stable textual form of the proto message.

#### Scenario: Unchanged mapper output passes
- **WHEN** the gRPC mappers run against the fixture and produce the same proto content as
  the approved snapshot
- **THEN** the golden-master tests pass

#### Scenario: Proto content change fails
- **WHEN** a refactoring changes any proto field value, repeated-field order or presence
- **THEN** the corresponding golden-master test fails

### Requirement: No orphaned snapshot files
Every `*.verified.*` file in a test project SHALL belong to an existing snapshot test.

#### Scenario: Orphan detection
- **WHEN** the test projects are inspected
- **THEN** there is no `*.verified.*` file without a test method that produces it

