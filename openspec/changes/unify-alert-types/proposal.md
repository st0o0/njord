## Why

Three parallel alert type hierarchies exist: `AlertType` (14 weather), `AirQualityAlertType` (2), `FloodAlertType` (2) — each with their own Alert record, Extensions class, and pipeline event. FunkArr has one pattern per concern. This triplication adds boilerplate, complicates consumers (3 switch arms instead of 1), and makes adding a new domain alert type require copying the entire pattern.

## What Changes

- **BREAKING**: Merge all alert types into one `AlertType` enum (Frost..WindChill + Pollen, Pm25, FloodRisk, FloodRain)
- One `Alert` record for all domains (carries AlertType, Severity, Confidence, Details)
- One `AlertRaised` PipelineEvent (replaces AlertRaised + AirQualityAlertRaised + FloodAlertRaised)
- One `IAlertRule` interface with `AlertType Type` and `Alert? Evaluate(...)` for all domains
- Update gRPC proto AlertType enum (already has all values, just clean up mapping)
- Update MQTT mappings (one path, not three)
- Update GrpcConsumer (one alert cache, not weather-only)

## Non-goals

- Splitting alerts into separate projects (that's the next change).
- Changing alert thresholds or logic.

## Capabilities

### New Capabilities
- `unified-alert-model`: One AlertType, one Alert record, one IAlertRule interface

### Modified Capabilities
- `weather-alert-projection`: Uses unified AlertType
- `threshold-alerts`: All alerts share one interface

## Impact

- **Njord.Messages**: AlertType unified, AirQualityAlertType/FloodAlertType removed
- **Njord.Alerts**: All rules implement one IAlertRule interface
- **Njord.Grpc**: One alert mapping path
- **Njord.Mqtt**: One alert payload path
- **Tests**: Simplified assertions (one type hierarchy)
