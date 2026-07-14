# consensus-computation Delta Specification

## MODIFIED Requirements

### Requirement: ConsensusResult is a pure data record without MQTT serialization
`ConsensusResult` SHALL be a record in `Njord.Domain.Analysis` holding location, per-horizon consensus metrics (median, trimmed mean, spread, IQR, agreement, outliers, confidence interval, model availability). It SHALL NOT contain `ToMqttMessages()` or reference `MqttMessage`, `TopicScheme`, or any type from `Njord.Mqtt`. MQTT serialization of `ConsensusResult` SHALL be the responsibility of `StatePayloadBuilder` in `Njord.Mqtt`.

#### Scenario: ConsensusResult has no MQTT dependency
- **WHEN** `ConsensusResult` is instantiated
- **THEN** it contains only domain data — no MQTT types are referenced

#### Scenario: MQTT serialization lives in StatePayloadBuilder
- **WHEN** a `ConsensusResult` needs to be published via MQTT
- **THEN** `StatePayloadBuilder.FromConsensus(result, baseTopic, location)` produces the `MqttMessage` instances
