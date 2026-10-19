## 1. Create Weather.Alerts project

- [ ] 1.1 Create `src/Njord.Weather.Alerts/` project (or rename existing Njord.Alerts)
- [ ] 1.2 Move 14 weather alert rules, WeatherAlertProjector, WeatherAlertEnrichment
- [ ] 1.3 Move AlertAnalyzer (pure compute) and AlertSettings
- [ ] 1.4 Add DI extension `AddWeatherAlerts()`
- [ ] 1.5 Create `src/Njord.Weather.Alerts.Tests/` with moved weather alert specs

## 2. Create AirQuality project

- [ ] 2.1 Create `src/Njord.AirQuality/` project
- [ ] 2.2 Move AQ alert rules, projector, enrichment from Njord.Alerts/AirQuality/
- [ ] 2.3 Move `OpenMeteoAirQualityMappings` from Njord.OpenMeteo/Mapping/
- [ ] 2.4 Move `AirQualityPayload` from Njord.Mqtt/Payloads/
- [ ] 2.5 Add DI extension `AddAirQuality()`
- [ ] 2.6 Create `src/Njord.AirQuality.Tests/` with moved AQ specs

## 3. Create Flood project

- [ ] 3.1 Create `src/Njord.Flood/` project
- [ ] 3.2 Move Flood alert rules, projectors, enrichments from Njord.Alerts/Flood/
- [ ] 3.3 Move `OpenMeteoFloodMappings` from Njord.OpenMeteo/Mapping/
- [ ] 3.4 Move `FloodPayload` from Njord.Mqtt/Payloads/
- [ ] 3.5 Add DI extension `AddFlood()`
- [ ] 3.6 Create `src/Njord.Flood.Tests/` with moved Flood specs

## 4. Delete old Njord.Alerts

- [ ] 4.1 Remove Njord.Alerts from solution (once all code moved)
- [ ] 4.2 Remove Njord.Alerts.Tests from solution
- [ ] 4.3 Update all project references

## 5. Update host

- [ ] 5.1 Replace `AnalysisSetupContainer` with per-domain setup containers
- [ ] 5.2 Host calls `AddWeatherAlerts()`, `AddAirQuality()`, `AddFlood()`

## 6. Update architecture tests

- [ ] 6.1 Update `LayerReferenceSpec` for new projects
- [ ] 6.2 Update `ZoneArchitectureSpec` — domain projects reference only Core
- [ ] 6.3 Update `NjordArchitecture` assembly list

## 7. Validation

- [ ] 7.1 `dotnet build Njord.slnx` — 0 errors
- [ ] 7.2 All test projects pass
