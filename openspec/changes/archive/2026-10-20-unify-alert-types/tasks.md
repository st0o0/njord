## 1. Unify AlertType enum

- [x] 1.1 Add Pollen, Pm25, FloodRisk, FloodRain to `AlertType` in `src/Njord.Messages/Alerts/`
- [x] 1.2 Delete `AirQualityAlertType` and `FloodAlertType` enums and their Extensions classes
- [x] 1.3 Update all references

## 2. Unify Alert record

- [x] 2.1 Ensure one `Alert` record works for all domains (same fields: Type, Severity, Confidence, Details)
- [x] 2.2 Delete `AirQualityAlert` and `FloodAlert` records if separate
- [x] 2.3 Update alert rule return types

## 3. Unify pipeline events

- [x] 3.1 Delete `AirQualityAlertRaised` and `FloodAlertRaised` from PipelineEvent
- [x] 3.2 All alert enrichments emit `AlertRaised` with the unified AlertType
- [x] 3.3 Update `PipelineEventMappings`

## 4. Unify IAlertRule interface

- [x] 4.1 Ensure all rules (weather, AQ, flood) implement one `IAlertRule` or equivalent
- [x] 4.2 Standardize the Evaluate signature across domains

## 5. Update consumers

- [x] 5.1 Update gRPC `WireEnumMappings` — one mapping for all AlertTypes
- [x] 5.2 Update gRPC `EnrichmentEventMappings` — one path for AlertRaised
- [x] 5.3 Update gRPC `GrpcConsumer` — one alert cache for all types
- [x] 5.4 Update MQTT `PipelineEventMqttMappings` — one alert path
- [x] 5.5 Update proto if needed (values 15-18 already exist)

## 6. Update tests

- [x] 6.1 Update alert rule tests for unified types
- [x] 6.2 Update gRPC mapping tests
- [x] 6.3 Update MQTT mapping tests
- [x] 6.4 Update pipeline tests

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx` — 0 errors
- [x] 7.2 All test projects pass
