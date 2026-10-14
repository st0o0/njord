## MODIFIED Requirements

### Requirement: Pipeline metrics
`PollManager` SHALL record on the `Njord.Pipeline` meter, from the poll workers' reports:
- `njord_poll_cycle_duration_seconds` (Histogram): duration of a poll cycle per location, from its first to its last model outcome, with label `location`.
- `njord_poll_cycle_models` (Gauge): number of models in the last completed cycle (every resolved model, whether its outcome was success or failure), with label `location`.
- `njord_poll_cycle_failed_models` (Gauge): number of models whose outcome in the last completed cycle was a failed fetch, with label `location`; 0 for a cycle without failures.
- `njord_pipeline_data_changed_total` (Counter): incremented when a worker reports a fetch result with a different hash than the previous one, with labels `location`, `model`.

#### Scenario: Poll cycle completion is recorded
- **WHEN** a poll cycle for location "lucerne" completes in 8.3 seconds with all 6 models reporting
- **THEN** `njord_poll_cycle_duration_seconds{location="lucerne"}` records 8.3
- **AND** `njord_poll_cycle_models{location="lucerne"}` is set to 6
- **AND** `njord_poll_cycle_failed_models{location="lucerne"}` is set to 0

#### Scenario: Data change is counted
- **WHEN** the poll worker of icon_d2 at lucerne reports a hash change
- **THEN** `njord_pipeline_data_changed_total{location="lucerne",model="icon_d2"}` is incremented by 1

#### Scenario: Failed models are recorded with the cycle
- **WHEN** a poll cycle for location "lucerne" with 2 models completes with one success and one failed report
- **THEN** `njord_poll_cycle_models{location="lucerne"}` is set to 2
- **AND** `njord_poll_cycle_failed_models{location="lucerne"}` is set to 1
