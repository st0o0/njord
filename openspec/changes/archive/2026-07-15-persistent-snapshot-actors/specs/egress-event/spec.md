## MODIFIED Requirements

### Requirement: EgressEvent is a protocol-neutral discriminated union

The system SHALL define `EgressEvent` as an abstract record in `Njord.Egress` with the following sealed variants:

- `PerModelUpdate(string Location, WeatherModel Model, ModelForecast Forecast)` — carries the typed domain forecast, not serialized JSON.
- `EnrichmentUpdate(string Location, string TypeName, object Result)` — unchanged.

#### Scenario: PerModelUpdate carries typed ModelForecast
- **WHEN** `ModelStateActor` produces a per-model update
- **THEN** it SHALL emit `EgressEvent.PerModelUpdate` with the `ModelForecast` domain object, not serialized horizon payloads

#### Scenario: EgressEvent carries domain data only
- **WHEN** an `EgressEvent` variant is constructed
- **THEN** it SHALL contain only domain types — no JSON strings, no MQTT references
