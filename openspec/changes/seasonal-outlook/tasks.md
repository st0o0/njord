## 1. Seasonal Client

- [ ] 1.1 Create `SeasonalSixHourlyData` and `SeasonalResponse` typed response models in `src/Njord.OpenMeteo/`
- [ ] 1.2 Add to `OpenMeteoJsonContext`
- [ ] 1.3 Create `ISeasonalClient` + `SeasonalClient` using `OpenMeteoRequestBuilder` with endpoint `seasonal-api.open-meteo.com/v1/seasonal`
- [ ] 1.4 Register in `OpenMeteoSetupContainer` with `AddStandardResilienceHandler`
- [ ] 1.5 Tests: URL building, response parsing

## 2. Seasonal Outlook Service

- [ ] 2.1 Create `ISeasonalOutlookStore` + `SeasonalOutlookStore` in `src/Njord.Core/Domain/` (same pattern as `IClimateBaselineStore`)
- [ ] 2.2 Create `SeasonalResponseMappings` in `src/Njord.OpenMeteo/Mapping/` — aggregate 6-hourly → monthly means (temp mean, precip sum per month)
- [ ] 2.3 Create `SeasonalOutlookService` hosted service in `src/Njord.OpenMeteo/` — fetches daily when enabled, compares with climate baseline, stores 3-month outlook
- [ ] 2.4 Confidence level from monthly temperature spread: <1.5°C High, <3.0°C Medium, else Low
- [ ] 2.5 Register hosted service in `OpenMeteoSetupContainer` (after `ClimateBaselineService`)
- [ ] 2.6 Tests: aggregation, anomaly comparison, confidence levels, retry on failure

## 3. Seasonal Outlook Handler

- [ ] 3.1 Create `MonthlyOutlook` record in `src/Njord.Messages/Pipeline/`
- [ ] 3.2 Create `SeasonalOutlookComputed` PipelineEvent
- [ ] 3.3 Create `SeasonalOutlookHandler` in `src/Njord.Weather/Lifestyle/` — triggers on Weather, reads store, emits 3-month outlook
- [ ] 3.4 Add `SeasonalOutlook` to `EnrichmentOptions` (default false)
- [ ] 3.5 Register as `IDomainHandler` in `AddWeatherDomain`
- [ ] 3.6 Tests: outlook emitted with data, no event when store empty

## 4. Wire + Consumer

- [ ] 4.1 Add `SeasonalOutlookComputed` to `LifestyleMappings.ToLifestyleUpdate` (flatten months to key-value)
- [ ] 4.2 Verify gRPC + MQTT consumers handle it via existing lifestyle pipeline
- [ ] 4.3 Tests: wire format

## 5. ha-njord

- [ ] 5.1 Add seasonal outlook sensor entity showing next month's temp/precip anomaly
- [ ] 5.2 Add `seasonal_outlook` enrichment toggle to config flow
- [ ] 5.3 Tests

## 6. Validation

- [ ] 6.1 `dotnet build Njord.slnx`
- [ ] 6.2 Affected test projects pass
- [ ] 6.3 Update AGENTS.md with seasonal outlook feature
