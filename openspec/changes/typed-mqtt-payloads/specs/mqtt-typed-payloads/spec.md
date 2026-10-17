## ADDED Requirements

### Requirement: Payloads are produced from typed records
Every MQTT state payload SHALL be produced by mapping a domain result to a sealed payload record and serializing it with the single shared serializer configuration. No MQTT payload SHALL be assembled with `JsonObject`, `JsonNode`, string concatenation or anonymous types.

#### Scenario: Alert payload
- **WHEN** an alert result is published
- **THEN** it is mapped to an alert payload record whose members are exactly the published keys

#### Scenario: Forecast horizon payload
- **WHEN** a forecast is projected for a horizon
- **THEN** the projection carries typed parameter values and the MQTT layer serializes it; `Njord.Weather` produces no JSON

### Requirement: Wire output is deterministic
The serialized key order and number formatting of every payload SHALL be fixed and covered by snapshot specs. Registry-driven keys (consensus parameters, trend parameters, forecast parameters) SHALL be derived through typed registry access in one place each.

#### Scenario: Unchanged output in the restructuring step
- **WHEN** the typed payload layer replaces the hand-built JSON
- **THEN** all existing golden masters for alerts, derived, trends, indices, consensus and per-model horizons are byte-identical

#### Scenario: Consensus key order
- **WHEN** a consensus horizon with several parameters is serialized
- **THEN** each parameter value is followed by its `_models` count and `_spread` and `_agreement` come last

### Requirement: Each mapper is specified field by field
Every payload mapper SHALL have a spec that asserts every payload member against an exact expected value, including rounding and absent values.

#### Scenario: Rounding
- **WHEN** a consensus median is 21.456
- **THEN** the payload value is 21.46

#### Scenario: Absent optional value
- **WHEN** an alert has no peak value
- **THEN** the payload member is null and serializes as `null`

### Requirement: Enum values use wire names
Enum-valued payload members SHALL serialize through the domain `ToWireName()` and never through `ToString()`.

#### Scenario: Severity text
- **WHEN** an alert with severity orange is serialized
- **THEN** the payload contains `"severity":"orange"`
