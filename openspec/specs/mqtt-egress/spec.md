# mqtt-egress Specification

## Purpose

MQTT state payload rules: values a model does not deliver are omitted from the flat JSON state payloads rather than published as placeholders.
## Requirements
### Requirement: Missing values are omitted from state payloads
Forecast horizon state payloads SHALL never carry placeholder values for missing data: a parameter whose value is null SHALL be omitted from its horizon's JSON, a horizon without any value or beyond the model's `MaxForecastHours` SHALL not be published, and an analysis feature that produces no result for a forecast (disabled, or trends without a previous consensus) SHALL publish nothing. Enrichment state payloads (alerts, derived, trends, indices, consensus) SHALL write an explicit `null` for a known member whose value is absent, uniformly for all of them. njord SHALL NOT publish `unavailable` states or `expire_after` settings itself; with MQTT Discovery removed, how consumers treat missing or old values is up to them. Availability SHALL be signalled only through the retained `online`/`offline` service status topic. `IceDetails.soil_frozen` SHALL be published as a JSON boolean.

#### Scenario: Unsupported parameter has no key
- **WHEN** `icon_d2` returns no value for `precipitation_probability`
- **THEN** the icon_d2 horizon payloads contain no `precipitation_probability` key

#### Scenario: Out-of-range horizon is not published
- **WHEN** a model's MaxForecastHours is 48 and h72 is configured
- **THEN** no h72 state message is published for that model

#### Scenario: Feature without a result publishes nothing
- **WHEN** a forecast is processed on the first cycle and trends have no previous consensus
- **THEN** no trends state message is published for that forecast

#### Scenario: Alert without optional timing values
- **WHEN** an alert has no peak value, no hours until and no duration
- **THEN** its payload contains `"peak_value":null`, `"hours_until":null` and `"duration_hours":null`

#### Scenario: Soil frozen is a boolean
- **WHEN** an ice alert reports frozen soil
- **THEN** the payload contains `"soil_frozen":true`

