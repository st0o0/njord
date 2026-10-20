# wire-format-golden-masters Specification

## Purpose
Pins the externally visible output of njord (MQTT state messages and gRPC analysis and
forecast responses) with snapshot tests, so internal refactorings can prove they changed
nothing a consumer sees.
## Requirements
### Requirement: No orphaned snapshot files
Every `*.verified.*` file in a test project SHALL belong to an existing snapshot test.

#### Scenario: Orphan detection
- **WHEN** the test projects are inspected
- **THEN** there is no `*.verified.*` file without a test method that produces it

