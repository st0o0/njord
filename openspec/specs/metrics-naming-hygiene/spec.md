## ADDED Requirements

### Requirement: All metrics follow njord_area_name convention

Every metric instrument SHALL follow the `njord_<area>_<name>` naming
pattern as defined in AGENTS.md. Counters SHALL end in `_total`, time
histograms in `_seconds`.

#### Scenario: OpenMeteo fetch counter
- **WHEN** the OpenMeteo fetch counter is recorded
- **THEN** the metric name SHALL be `njord_openmeteo_fetch_total`

#### Scenario: OpenMeteo fetch duration
- **WHEN** the OpenMeteo fetch duration is recorded
- **THEN** the metric name SHALL be `njord_openmeteo_fetch_duration_seconds`

#### Scenario: Pipeline data-changed counter
- **WHEN** the pipeline data-changed counter is recorded
- **THEN** the metric name SHALL be `njord_pipeline_data_changed_total`

### Requirement: No dead metric instruments

Metric instruments that are defined but never recorded to SHALL NOT exist
in production code.

#### Scenario: ThrottleWait histogram removed
- **WHEN** inspecting `Njord.Pipeline/Telemetry.cs`
- **THEN** the `ThrottleWait` histogram SHALL not be present (throttle moved to OpenMeteo)
