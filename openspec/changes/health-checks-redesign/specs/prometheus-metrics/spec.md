## MODIFIED Requirements

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
