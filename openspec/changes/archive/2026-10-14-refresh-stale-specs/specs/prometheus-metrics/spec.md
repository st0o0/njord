## MODIFIED Requirements

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

Budget values SHALL be published via `NjordHealthMonitor.Budget` — `BudgetTracker` writes the usage after each persisted `ApiCallRecorded`, `ConfigManager` updates the monthly limit on a budget change, and the gauge callbacks read the snapshot.

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
`PollScheduler` SHALL record on the `Njord.Pipeline` meter:
- `njord_poll_cycle_duration_seconds` (Histogram): duration of a poll cycle per location, with label `location`.
- `njord_poll_cycle_models` (Gauge): number of models that reported in the last completed cycle, with label `location`.
- `njord_pipeline_data_changed_total` (Counter): incremented when a fetch result has a different hash than the previous one, with labels `location`, `model`.

#### Scenario: Poll cycle completion is recorded
- **WHEN** a poll cycle for location "lucerne" completes in 8.3 seconds with all 6 models reporting
- **THEN** `njord_poll_cycle_duration_seconds{location="lucerne"}` records 8.3
- **AND** `njord_poll_cycle_models{location="lucerne"}` is set to 6

#### Scenario: Data change is counted
- **WHEN** `PollScheduler` detects a hash change for icon_d2 at lucerne
- **THEN** `njord_pipeline_data_changed_total{location="lucerne",model="icon_d2"}` is incremented by 1

### Requirement: Egress metrics
The MQTT layer SHALL record on the `Njord.Mqtt` meter:
- `njord_mqtt_dedup_total` (Counter): incremented on each publish decision of `MqttStatePublisher`, with labels `location`, `decision` (published, skipped).
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

## REMOVED Requirements

### Requirement: NjordMetrics singleton owns the Meter
**Reason**: There is no `NjordMetrics` class and no shared `"Njord"` meter. Each feature library owns an `internal static class Telemetry` with its own `Njord.<Domain>` meter.
**Migration**: See `per-domain-telemetry`.

### Requirement: Enrichment metrics
**Reason**: No enrichment, consensus or history instruments exist; the registered meters are `Njord.OpenMeteo`, `Njord.Budget`, `Njord.Pipeline` and `Njord.Mqtt`.
**Migration**: None.
