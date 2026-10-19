## MODIFIED Requirements

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
