## Capability: consensus-horizon-advance

Time-adjusted horizon selection for consensus weather entities. As hours pass since the consensus was computed, the entity advances through horizons so that current state always reflects the most relevant forecast.

### Requirements

#### Requirement: EnrichmentData tracks consensus computation timestamp
`EnrichmentData` SHALL have a `consensus_updated_at: datetime | None` field that records when the consensus data was computed by njord.

##### Scenario: Stream event with consensus payload
- **WHEN** an `EnrichmentEvent` with payload type `consensus` and `updated_at = 2026-07-15T14:00:00Z` is received
- **THEN** the resulting `EnrichmentData` has `consensus_updated_at = 2026-07-15T14:00:00Z`

##### Scenario: Stream event with non-consensus payload
- **WHEN** an `EnrichmentEvent` with payload type `indices` is received
- **THEN** the resulting `EnrichmentData` has `consensus_updated_at = None`

##### Scenario: Unary GetEnrichments response
- **WHEN** `GetEnrichments` returns a response with consensus data
- **THEN** the resulting `EnrichmentData` has `consensus_updated_at` set to approximately `utcnow()`

##### Scenario: Merge preserves timestamp on non-consensus event
- **WHEN** an existing `EnrichmentData` has `consensus_updated_at = 14:00`
- **AND** a new event with payload type `alerts` is merged
- **THEN** the merged `EnrichmentData` retains `consensus_updated_at = 14:00`

##### Scenario: Merge updates timestamp on consensus event
- **WHEN** an existing `EnrichmentData` has `consensus_updated_at = 14:00`
- **AND** a new event with payload type `consensus` and `updated_at = 17:00` is merged
- **THEN** the merged `EnrichmentData` has `consensus_updated_at = 17:00`

#### Requirement: Consensus entity selects horizon based on elapsed time
The consensus entity SHALL calculate the number of full hours elapsed since `consensus_updated_at` and use `h{elapsed}` as the current horizon instead of hardcoded `h0`.

##### Scenario: Consensus pushed at 14:00, current time is 16:30
- **WHEN** `consensus_updated_at` is `14:00` and the current time is `16:30`
- **THEN** the entity reads `h2` for current state (temperature, condition, etc.)

##### Scenario: Consensus pushed at 14:00, current time is 14:10
- **WHEN** `consensus_updated_at` is `14:00` and the current time is `14:10`
- **THEN** the entity reads `h0` for current state

##### Scenario: Elapsed hours exceeds available horizons
- **WHEN** consensus data has horizons h0-h48 and 50 hours have elapsed
- **THEN** the entity returns `None` for all current-state properties (shows "Unknown")

##### Scenario: No consensus_updated_at available
- **WHEN** `consensus_updated_at` is `None`
- **THEN** the entity falls back to `h0` (offset = 0)

#### Requirement: Consensus forecast horizons shift with elapsed time
`_async_forecast_hourly()` SHALL start from `h{elapsed+1}` instead of `h1`. `_async_forecast_daily()` SHALL apply the same offset when grouping horizons into calendar days.

##### Scenario: Forecast after 3 hours elapsed
- **WHEN** 3 full hours have elapsed since consensus was computed
- **THEN** `_async_forecast_hourly()` returns entries starting from `h4` (not `h1`)

#### Requirement: Extra state attributes use adjusted horizon
The `agreement`, `spread`, `available_models`, and `reliable_hours` extra state attributes SHALL be calculated from the time-adjusted horizon instead of always using `h0`.

##### Scenario: Agreement from adjusted horizon
- **WHEN** 2 hours have elapsed since consensus computation
- **THEN** `agreement` in extra_state_attributes comes from the `h2` temperature consensus, not `h0`

##### Scenario: Reliable hours counted from adjusted horizon
- **WHEN** 2 hours have elapsed and temperature agreement drops below 0.5 at `h10`
- **THEN** `reliable_hours` is 8 (from `h2` through `h9`, not from `h0`)
