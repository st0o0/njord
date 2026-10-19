## 1. Create Weather.Alerts project

- [x] 1.1 Create `src/Njord.Weather.Alerts/` project (or rename existing Njord.Alerts)
- [x] 1.2 Move 14 weather alert rules, WeatherAlertProjector, WeatherAlertEnrichment
- [x] 1.3 Move AlertAnalyzer (pure compute) and AlertSettings
- [x] 1.4 Add DI extension `AddWeatherAlerts()`
- [x] 1.5 Create `src/Njord.Weather.Alerts.Tests/` with moved weather alert specs

## 2. Create AirQuality project

- [x] 2.1 Create `src/Njord.AirQuality/` project
- [x] 2.2 Move AQ alert rules, projector, enrichment from Njord.Alerts/AirQuality/
- [ ] 2.3 Move `OpenMeteoAirQualityMappings` from Njord.OpenMeteo/Mapping/ (deferred: moving it would make OpenMeteo or Mqtt reference a domain project, violating the lateral rule)
- [ ] 2.4 Move `AirQualityPayload` from Njord.Mqtt/Payloads/ (deferred: moving it would make OpenMeteo or Mqtt reference a domain project, violating the lateral rule)
- [x] 2.5 Add DI extension `AddAirQuality()`
- [x] 2.6 Create `src/Njord.AirQuality.Tests/` with moved AQ specs

## 3. Create Flood project

- [x] 3.1 Create `src/Njord.Flood/` project
- [x] 3.2 Move Flood alert rules, projectors, enrichments from Njord.Alerts/Flood/
- [ ] 3.3 Move `OpenMeteoFloodMappings` from Njord.OpenMeteo/Mapping/ (deferred: moving it would make OpenMeteo or Mqtt reference a domain project, violating the lateral rule)
- [ ] 3.4 Move `FloodPayload` from Njord.Mqtt/Payloads/ (deferred: moving it would make OpenMeteo or Mqtt reference a domain project, violating the lateral rule)
- [x] 3.5 Add DI extension `AddFlood()`
- [x] 3.6 Create `src/Njord.Flood.Tests/` with moved Flood specs

## 4. Delete old Njord.Alerts

- [x] 4.1 Remove Njord.Alerts from solution (once all code moved) (kept Njord.Alerts as the thin shared alert kernel; its remaining spec project stays)
- [x] 4.2 Remove Njord.Alerts.Tests from solution (kept Njord.Alerts as the thin shared alert kernel; its remaining spec project stays)
- [x] 4.3 Update all project references

## 5. Update host

- [x] 5.1 Replace `AnalysisSetupContainer` with per-domain setup containers
- [x] 5.2 Host calls `AddWeatherAlerts()`, `AddAirQuality()`, `AddFlood()`

## 6. Update architecture tests

- [x] 6.1 Update `LayerReferenceSpec` for new projects
- [x] 6.2 Update `ZoneArchitectureSpec` — domain projects reference only Core
- [x] 6.3 Update `NjordArchitecture` assembly list

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx` — 0 errors
- [x] 7.2 All test projects pass
