## MODIFIED Requirements

### Requirement: Horizon capping uses ModelCoverageRegistry
When `MqttStatePublisher` projects a forecast to per-horizon payloads, it SHALL pass the model's `MaxForecastHours` from `ModelCoverageRegistry` to `HorizonProjection.BuildPerHorizon`. Hourly horizons exceeding `MaxForecastHours` SHALL be excluded. Daily day-offsets SHALL be limited to `min(forecastDays, ceil(MaxForecastHours / 24))` days. For models not in the registry (unknown), all configured horizons and forecast days SHALL be included.

#### Scenario: Short-range model excludes far horizons
- **WHEN** a model has MaxForecastHours=48, configured horizons are [3, 6, 12, 24, 48, 72] and forecast days are 4
- **THEN** applicable horizons are [3, 6, 12, 24, 48] and applicable day-offsets are [0, 1]

#### Scenario: Long-range model includes all horizons
- **WHEN** `ecmwf_ifs025` has MaxForecastHours=362, configured horizons are [3, 6, 12, 24, 48, 72] and forecast days are 4
- **THEN** applicable horizons are [3, 6, 12, 24, 48, 72] and applicable day-offsets are [0, 1, 2, 3]

#### Scenario: Unknown model includes all horizons
- **WHEN** a model not in `ModelCoverageRegistry` is configured with horizons [3, 6, 12, 24, 48, 72]
- **THEN** applicable horizons include all configured horizons

## REMOVED Requirements

### Requirement: ModelStateActor learns parameter support from API responses
**Reason**: There is no runtime capability learning; `ModelStateActor` was removed and nothing tracks which parameters a model delivered.
**Migration**: Null values are omitted from state payloads instead; see `delta-publishing`.

### Requirement: ModelStateActor emits CapabilityLearned
**Reason**: `EgressEvent.CapabilityLearned` and the actor that emitted it no longer exist; capability events only fed the removed MQTT Discovery.
**Migration**: None.

### Requirement: CapabilityLearned is a full-state idempotent message
**Reason**: The `CapabilityLearned` record no longer exists.
**Migration**: None.
