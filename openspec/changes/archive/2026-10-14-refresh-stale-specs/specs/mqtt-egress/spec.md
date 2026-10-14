## ADDED Requirements

### Requirement: Missing values are omitted from state payloads
State payloads SHALL never carry placeholder values for missing data: a parameter whose value is null SHALL be omitted from its horizon's JSON, a horizon without any value or beyond the model's `MaxForecastHours` SHALL not be published, and an analysis feature that produces no result for a forecast (disabled, or trends without a previous consensus) SHALL publish nothing. njord SHALL NOT publish `unavailable` states or `expire_after` settings itself; with MQTT Discovery removed, how consumers treat missing or old values is up to them. Availability SHALL be signalled only through the retained `online`/`offline` service status topic.

#### Scenario: Unsupported parameter has no key
- **WHEN** `icon_d2` returns no value for `precipitation_probability`
- **THEN** the icon_d2 horizon payloads contain no `precipitation_probability` key

#### Scenario: Out-of-range horizon is not published
- **WHEN** a model's MaxForecastHours is 48 and h72 is configured
- **THEN** no h72 state message is published for that model

#### Scenario: Feature without a result publishes nothing
- **WHEN** a forecast is processed on the first cycle and trends have no previous consensus
- **THEN** no trends state message is published for that forecast

## REMOVED Requirements

### Requirement: Missing values surface as unavailable, never as stale data
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Missing values are omitted from state payloads" in this capability.
