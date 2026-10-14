## MODIFIED Requirements

### Requirement: Poll cycle summary is logged at Debug level
`PollScheduler` SHALL track, per location, one outcome per resolved model of the current cycle: changed data, unchanged data, or failed. A `FetchForecastCompleted` SHALL count as changed or unchanged, and a `FetchForecastFailed` of any reason SHALL count as a final failed outcome for the current cycle; the cycle SHALL NOT wait for the retry of a failed model. A later outcome of a model whose cycle is still open SHALL replace that model's earlier outcome. When every resolved model of the location has an outcome, the cycle completes: `PollScheduler` SHALL record `njord_poll_cycle_duration_seconds`, `njord_poll_cycle_models` and `njord_poll_cycle_failed_models` and emit a Debug-level summary `"Poll complete for {Location}: {Changed}/{Total} models changed, {Failed} failed in {Duration}ms"`, and the next outcome SHALL open a new cycle. Outcomes for a location or model that is not configured SHALL open no cycle, so at most one cycle per configured location is open at any time. Failed fetches SHALL additionally be logged separately at Warning level.

#### Scenario: All models return data
- **WHEN** a poll cycle for location "vreden" completes with 10 models reporting and 3 of them changed
- **THEN** a Debug log `"Poll complete for vreden: 3/10 models changed, 0 failed in {Duration}ms"` is emitted

#### Scenario: Failed fetch is logged as warning
- **WHEN** a fetch for (vreden, icon_d2) fails with `Transport`
- **THEN** a Warning log naming the location, model and failure detail is emitted

#### Scenario: A cycle with a failed model completes
- **WHEN** location "lucerne" resolves models A and B, A reports changed data and B reports `FetchForecastFailed`
- **THEN** the cycle completes with 2 models, 1 changed and 1 failed, and a Debug log `"Poll complete for lucerne: 1/2 models changed, 1 failed in {Duration}ms"` is emitted

#### Scenario: Consecutive cycles with failures stay separate
- **WHEN** a cycle of models A, B and C completes with C failed, and afterwards A, B and C all report again
- **THEN** two cycles complete, each with 3 models, the first with 1 failed and the second with 0 failed

#### Scenario: A retry before completion replaces the failed outcome
- **WHEN** model A fails, A's retry succeeds while model B is still outstanding, and then B reports
- **THEN** the cycle completes with 2 models and 0 failed

#### Scenario: Outcomes for unknown models open no cycle
- **WHEN** a `FetchForecastFailed` arrives for a model that is not resolved for the location
- **THEN** no cycle is opened and no summary is emitted
