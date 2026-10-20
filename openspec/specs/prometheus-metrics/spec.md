## Purpose

Exposes njord runtime metrics through one `Meter` per feature library (`Njord.OpenMeteo`, `Njord.Pipeline`, `Njord.Mqtt`, ...) and prometheus-net export, with bounded naming and tag conventions.
## Requirements
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
`OpenMeteoClient` SHALL record two instruments on the `Njord.OpenMeteo` meter:
- `njord_openmeteo_fetch_total` (Counter): incremented after each Open-Meteo API call with labels `location`, `model`, `outcome` (`success`, or the lower-cased `PollFailureReason`: `ratelimited`, `modelunavailable`, `malformedpayload`, `transport`).
- `njord_openmeteo_fetch_duration_seconds` (Histogram): elapsed time of successful fetches with labels `location`, `model`.

#### Scenario: Successful fetch is recorded
- **WHEN** `OpenMeteoClient.FetchAsync` returns a successful outcome in 1.2 seconds
- **THEN** `njord_openmeteo_fetch_total{location="lucerne",model="icon_d2",outcome="success"}` is incremented by 1
- **AND** `njord_openmeteo_fetch_duration_seconds{location="lucerne",model="icon_d2"}` records 1.2

#### Scenario: Failed fetch records outcome without duration
- **WHEN** `OpenMeteoClient.FetchAsync` returns a rate-limited outcome
- **THEN** `njord_openmeteo_fetch_total{location="lucerne",model="icon_d2",outcome="ratelimited"}` is incremented by 1
- **AND** no duration is recorded

### Requirement: Budget metrics
The budget layer SHALL expose four observable gauges on the `Njord.Budget` meter, registered once through `BudgetTelemetry.Observe`, and the gateway SHALL record one histogram on the `Njord.OpenMeteo` meter:
- `njord_budget_used_daily` (ObservableGauge): current daily API request count.
- `njord_budget_used_monthly` (ObservableGauge): current monthly API request count.
- `njord_budget_limit_daily` (ObservableGauge): daily limit.
- `njord_budget_limit_monthly` (ObservableGauge): resolved monthly limit.
- `njord_gateway_throttle_wait_seconds` (Histogram): time a fetch target was parked by `GatewayThrottleStage` before being released.

Budget usage SHALL be published by `BudgetTracker` into the `BudgetUsageState` owned by `Njord.Budget` (after recovery and after each persisted `ApiCallRecorded`). The used gauges read that state and the limit gauges read the effective budget from the current polling configuration (`ConfigView`), so a budget change is visible without any component pushing limits.

#### Scenario: Budget gauges reflect actor state
- **WHEN** `BudgetTracker` has recorded 150 requests today and 4200 this month
- **THEN** `njord_budget_used_daily` reports 150
- **AND** `njord_budget_used_monthly` reports 4200

#### Scenario: Budget limits are exposed
- **WHEN** the service starts with free-tier defaults
- **THEN** `njord_budget_limit_daily` reports 10000
- **AND** `njord_budget_limit_monthly` reports 300000

#### Scenario: Throttle wait is recorded
- **WHEN** `GatewayThrottleStage` parks a fetch target for 3.5 seconds before releasing it
- **THEN** `njord_gateway_throttle_wait_seconds` records 3.5

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

### Requirement: Egress metrics
The MQTT layer SHALL record on the `Njord.Mqtt` meter:
- `njord_mqtt_dedup_total` (Counter): incremented on each publish decision of `MqttStatePublisher`, with labels `location`, `decision` (published, skipped). `published` SHALL be counted only after the message was sent successfully.
- `njord_mqtt_publish_failures_total` (Counter): incremented for each state message whose publish failed or timed out, with label `location`.
- `njord_mqtt_connected` (Gauge): 1 when `MqttConnection` is connected, 0 after a disconnect. When MQTT is disabled the gauge is never recorded.

#### Scenario: Dedup decision is counted
- **WHEN** `MqttStatePublisher` skips a publish because the payload hash matches
- **THEN** `njord_mqtt_dedup_total{location="lucerne",decision="skipped"}` is incremented by 1

#### Scenario: New data is published
- **WHEN** `MqttStatePublisher` publishes because the payload hash differs
- **THEN** `njord_mqtt_dedup_total{location="lucerne",decision="published"}` is incremented by 1

#### Scenario: MQTT connection state is exposed
- **WHEN** `MqttConnection` connects successfully
- **THEN** `njord_mqtt_connected` is set to 1

#### Scenario: MQTT disconnection is exposed
- **WHEN** `MqttConnection` loses the connection
- **THEN** `njord_mqtt_connected` is set to 0

#### Scenario: Publish failure is counted
- **WHEN** the publish of a state message for location "lucerne" fails
- **THEN** `njord_mqtt_publish_failures_total{location="lucerne"}` is incremented by 1
- **AND** `njord_mqtt_dedup_total{location="lucerne",decision="published"}` is not incremented

### Requirement: Metric naming follows Prometheus conventions
All instrument names SHALL use snake_case with an `njord_` prefix. Instruments
measuring time SHALL use the `_seconds` suffix. Instruments counting events
SHALL use the `_total` suffix. Labels SHALL be low-cardinality (location slug,
model id, outcome enum, feature name, decision enum).

#### Scenario: Names are Prometheus-compliant
- **WHEN** the `/metrics` endpoint is scraped
- **THEN** all njord metrics use `njord_` prefix, snake_case names, and
  appropriate unit suffixes

