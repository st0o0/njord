## ADDED Requirements

### Requirement: NjordMetrics singleton owns the Meter
A `NjordMetrics` class SHALL expose a static `Instance` property holding a
`System.Diagnostics.Metrics.Meter` named `"Njord"`. The constructor SHALL be
private. All njord instruments SHALL be created through extension methods on
`NjordMetrics`.

#### Scenario: Single Meter instance
- **WHEN** any component accesses `NjordMetrics.Instance`
- **THEN** it receives the same `NjordMetrics` instance with a Meter named `"Njord"`

#### Scenario: Instruments are created via extensions
- **WHEN** a component calls `NjordMetrics.Instance.AddFetchTotal()`
- **THEN** a `Counter<long>` named `njord_fetch_total` is returned from the shared Meter

### Requirement: Prometheus scrape endpoint
The service SHALL expose a `/metrics` endpoint on the existing Kestrel HTTP
port that returns metrics in Prometheus exposition format. The endpoint SHALL
include .NET runtime metrics (GC, ThreadPool, process) in addition to
njord-specific instruments.

#### Scenario: Metrics endpoint responds
- **WHEN** Alloy scrapes `GET /metrics`
- **THEN** the response is `200 OK` with `text/plain` content in Prometheus
  exposition format containing njord and runtime metrics

### Requirement: Ingest metrics
The ingest layer SHALL record two instruments:
- `njord_fetch_total` (Counter): incremented after each Open-Meteo API call
  with labels `location`, `model`, `outcome` (success, rate_limited, transport,
  model_unavailable).
- `njord_fetch_duration_seconds` (Histogram): elapsed time of successful fetches
  with labels `location`, `model`.

#### Scenario: Successful fetch is recorded
- **WHEN** `OpenMeteoClient.FetchAsync` returns a successful outcome in 1.2 seconds
- **THEN** `njord_fetch_total{location="lucerne",model="icon_d2",outcome="success"}` is incremented by 1
- **AND** `njord_fetch_duration_seconds{location="lucerne",model="icon_d2"}` records 1.2

#### Scenario: Failed fetch records outcome without duration
- **WHEN** `OpenMeteoClient.FetchAsync` returns a `rate_limited` outcome
- **THEN** `njord_fetch_total{location="lucerne",model="icon_d2",outcome="rate_limited"}` is incremented by 1
- **AND** no duration is recorded

### Requirement: Budget metrics
The budget layer SHALL expose four observable gauges and one histogram:
- `njord_budget_used_daily` (ObservableGauge): current daily API request count.
- `njord_budget_used_monthly` (ObservableGauge): current monthly API request count.
- `njord_budget_limit_daily` (ObservableGauge): configured daily limit.
- `njord_budget_limit_monthly` (ObservableGauge): configured monthly limit.
- `njord_throttle_wait_seconds` (Histogram): time a request was parked by
  `BudgetThrottleStage` before being released.

Budget values SHALL be published via `NjordHealthState` — the
`BudgetTrackerActor` writes them, the ObservableGauge callbacks read them.

#### Scenario: Budget gauges reflect actor state
- **WHEN** `BudgetTrackerActor` has processed 150 requests today and 4200 this month
- **THEN** `njord_budget_used_daily` reports 150
- **AND** `njord_budget_used_monthly` reports 4200

#### Scenario: Budget limits are exposed
- **WHEN** the service starts with free-tier defaults
- **THEN** `njord_budget_limit_daily` reports 10000
- **AND** `njord_budget_limit_monthly` reports 300000

#### Scenario: Throttle wait is recorded
- **WHEN** `BudgetThrottleStage` parks a request for 3.5 seconds before releasing it
- **THEN** `njord_throttle_wait_seconds` records 3.5

### Requirement: Pipeline metrics
The pipeline layer SHALL record:
- `njord_poll_cycle_duration_seconds` (Histogram): total duration of a poll
  cycle per location, with label `location`.
- `njord_poll_cycle_models` (Gauge): number of models that reported in the
  last completed cycle, with label `location`.
- `njord_data_changed_total` (Counter): incremented when a fetch result has a
  different hash than the previous one, with labels `location`, `model`.

#### Scenario: Poll cycle completion is recorded
- **WHEN** a poll cycle for location "lucerne" completes in 8.3 seconds with
  6 of 8 models responding
- **THEN** `njord_poll_cycle_duration_seconds{location="lucerne"}` records 8.3
- **AND** `njord_poll_cycle_models{location="lucerne"}` is set to 6

#### Scenario: Data change is counted
- **WHEN** `SchedulerActor` detects a hash change for icon_d2 at lucerne
- **THEN** `njord_data_changed_total{location="lucerne",model="icon_d2"}` is incremented by 1

### Requirement: Enrichment metrics
The enrichment layer SHALL record:
- `njord_enrichment_duration_seconds` (Histogram): computation time per
  enrichment feature, with labels `location`, `feature`.
- `njord_consensus_models` (Gauge): number of models included in the last
  consensus computation, with label `location`.
- `njord_consensus_spread_celsius` (Gauge): temperature spread across models
  in the last consensus, with label `location`.
- `njord_history_mae_celsius` (Gauge): mean absolute error from the history
  enrichment, with labels `location`, `model`.
- `njord_history_model_weight` (Gauge): accuracy-derived model weight from
  the history enrichment, with labels `location`, `model`.

#### Scenario: Enrichment duration is recorded per feature
- **WHEN** the alerts enrichment for "lucerne" completes in 0.02 seconds
- **THEN** `njord_enrichment_duration_seconds{location="lucerne",feature="alerts"}` records 0.02

#### Scenario: Consensus quality is exposed
- **WHEN** consensus for "lucerne" uses 6 models with a 2.3°C temperature spread
- **THEN** `njord_consensus_models{location="lucerne"}` is set to 6
- **AND** `njord_consensus_spread_celsius{location="lucerne"}` is set to 2.3

#### Scenario: History MAE is exposed per model
- **WHEN** the history enrichment computes a 7-day MAE of 1.8°C for icon_d2
  at lucerne with weight 0.22
- **THEN** `njord_history_mae_celsius{location="lucerne",model="icon_d2"}` is set to 1.8
- **AND** `njord_history_model_weight{location="lucerne",model="icon_d2"}` is set to 0.22

### Requirement: Egress metrics
The egress layer SHALL record:
- `njord_mqtt_dedup_total` (Counter): incremented on each publish decision,
  with labels `location`, `decision` (published, skipped).
- `njord_mqtt_connected` (Gauge): 1 when MQTT is connected, 0 when
  disconnected. Set to 0 when MQTT is disabled.

#### Scenario: Dedup decision is counted
- **WHEN** `MqttEgressActor` skips a publish because the payload hash matches
- **THEN** `njord_mqtt_dedup_total{location="lucerne",decision="skipped"}` is incremented by 1

#### Scenario: New data is published
- **WHEN** `MqttEgressActor` publishes because the payload hash differs
- **THEN** `njord_mqtt_dedup_total{location="lucerne",decision="published"}` is incremented by 1

#### Scenario: MQTT connection state is exposed
- **WHEN** `MqttConnectionActor` connects successfully
- **THEN** `njord_mqtt_connected` is set to 1

#### Scenario: MQTT disconnection is exposed
- **WHEN** `MqttConnectionActor` loses the connection
- **THEN** `njord_mqtt_connected` is set to 0

### Requirement: Metric naming follows Prometheus conventions
All instrument names SHALL use snake_case with an `njord_` prefix. Instruments
measuring time SHALL use the `_seconds` suffix. Instruments counting events
SHALL use the `_total` suffix. Labels SHALL be low-cardinality (location slug,
model id, outcome enum, feature name, decision enum).

#### Scenario: Names are Prometheus-compliant
- **WHEN** the `/metrics` endpoint is scraped
- **THEN** all njord metrics use `njord_` prefix, snake_case names, and
  appropriate unit suffixes
