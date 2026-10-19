# mqtt-typed-payloads Specification

## Purpose
Typed MQTT payload records for multi-domain weather data. Each domain (Weather, AirQuality, Flood, Solar, Marine) and alert type has its own payload record, serialized by a shared source-generated JSON context. Old consensus, derived, trend, and index payload types are removed.
## Requirements
### Requirement: Payloads are produced from typed records
Every MQTT state payload SHALL be produced by mapping a domain result to a sealed payload record and serializing it with the single shared serializer configuration. No MQTT payload SHALL be assembled with `JsonObject`, `JsonNode`, string concatenation or anonymous types.

#### Scenario: Alert payload
- **WHEN** an alert result is published
- **THEN** it is mapped to an alert payload record whose members are exactly the published keys

#### Scenario: Domain data payload
- **WHEN** weather domain data is published
- **THEN** the data is mapped to a WeatherPayload record with ensemble mean values and optional std dev

### Requirement: Domain-specific payload records
MQTT payloads SHALL be typed records per domain: WeatherPayload, AirQualityPayload, FloodPayload, SolarPayload. Old consensus and derived payload types are removed.

#### Scenario: Weather payload contains ensemble data
- **WHEN** a weather payload is published
- **THEN** it SHALL contain mean values and optional std dev for each parameter

### Requirement: Alert payload records
Alert payloads SHALL carry AlertType, Severity, Confidence, and domain-specific details. Old AlertComputed payload format is replaced.

#### Scenario: Alert payload with confidence
- **WHEN** a frost alert is published to MQTT
- **THEN** the payload SHALL include severity, confidence level, peak value, and hours until onset

### Requirement: Wire output is deterministic
The serialized key order and number formatting of every payload SHALL be fixed and covered by snapshot specs. Registry-driven keys (consensus parameters, trend parameters, forecast parameters) SHALL be derived through typed registry access in one place each.

#### Scenario: Domain data key order is stable
- **WHEN** a weather domain payload is serialized
- **THEN** the key order SHALL be fixed and covered by snapshot specs

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

