# set-enrichment-atomic-update Specification

## Purpose
TBD - created by archiving change improve-set-enrichment. Update Purpose after archive.
## Requirements
### Requirement: SetEnrichment applies all sections or none
`SetEnrichment` SHALL validate every touched section and persist them in a single override-file write followed by one configuration reload. If any section fails validation, nothing SHALL be persisted.

#### Scenario: One invalid section blocks the others
- **WHEN** a client sends valid `consensus` settings together with `alerts.frost_thresholds = [0, 5, -15]`
- **THEN** the response has `applied = false`, the consensus setting is unchanged, and the override file is untouched

#### Scenario: Several valid sections
- **WHEN** a client sends valid `consensus` and `indices` settings in one request
- **THEN** both are persisted by one write and `GetConfig` shows both

#### Scenario: Persistence failure part-way
- **WHEN** the override file cannot be written
- **THEN** no section is applied and the call fails as a whole

### Requirement: Structured issues and restart requirements
`ConfigResponse` SHALL carry `repeated ConfigIssue issues` (each with the `EnrichmentSection` and a message) for validation failures and `repeated string restart_required` for settings that take effect after a restart. `rejection_reason` SHALL still contain the joined human-readable messages. Restart hints SHALL NOT appear in `warnings`.

#### Scenario: Validation failure names the section
- **WHEN** `alerts` fails validation
- **THEN** `issues` contains an entry with section `ALERTS` and the validator message

#### Scenario: Toggle needs a restart
- **WHEN** a client enables `trends`
- **THEN** `restart_required` names the trend setting and `warnings` does not contain a restart hint

### Requirement: Reset a section to its defaults
`SetEnrichmentRequest.reset` SHALL list sections whose override is removed, so the base configuration (appsettings, environment) applies again. Resets SHALL be applied before the updates of the same request. `ENRICHMENT_SECTION_UNSPECIFIED` in `reset` SHALL be reported as an issue.

#### Scenario: Reset alert thresholds
- **WHEN** alert thresholds were overridden and a client sends `reset: [ALERTS]`
- **THEN** the alert override is removed and `GetConfig` reports the base configuration values

#### Scenario: Reset and update together
- **WHEN** a client sends `reset: [ALERTS]` and `alerts.enabled = false`
- **THEN** the alert section is reset first and then disabled

### Requirement: Validate only
With `SetEnrichmentRequest.dry_run = true` the service SHALL validate the request and return the result without persisting or reloading. The response SHALL have `applied = false`, `dry_run = true` and the issues found; the request is valid iff `issues` is empty.

#### Scenario: Valid dry run
- **WHEN** a valid request is sent with `dry_run = true`
- **THEN** `issues` is empty, nothing is persisted and `GetConfig` is unchanged

#### Scenario: Invalid dry run
- **WHEN** an invalid request is sent with `dry_run = true`
- **THEN** `issues` lists the failures and nothing is persisted

### Requirement: Empty request is rejected
A `SetEnrichmentRequest` with no section and no reset SHALL be rejected with an issue stating that at least one section is required.

#### Scenario: Request without content
- **WHEN** a client sends an empty `SetEnrichmentRequest`
- **THEN** the response has `applied = false` and one issue; nothing changes

