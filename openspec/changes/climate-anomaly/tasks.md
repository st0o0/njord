## 1. Climate Client

- [ ] 1.1 Create `ClimateResponse` + `ClimateDailyData` typed response models in Njord.OpenMeteo
- [ ] 1.2 Create `IClimateClient` + `ClimateClient` targeting climate-api.open-meteo.com
- [ ] 1.3 Add to OpenMeteoJsonContext and DI registration
- [ ] 1.4 Tests for URL building and response parsing

## 2. Climate Baseline Service

- [ ] 2.1 Create `MonthlyBaseline` record (MeanTemp, MeanPrecip, MeanSunshine, Month)
- [ ] 2.2 Create `ClimateBaselineService` (BackgroundService) that fetches 30 years per location, aggregates to 12 monthly averages
- [ ] 2.3 Cache as `IReadOnlyDictionary<string, MonthlyBaseline[]>` singleton
- [ ] 2.4 Daily refresh timer
- [ ] 2.5 Tests for aggregation logic (given daily data, compute monthly means)

## 3. Climate Anomaly Handler

- [ ] 3.1 Add `ClimateAnomaly` bool to EnrichmentOptions (default false)
- [ ] 3.2 Create `ClimateAnomalyComputed` PipelineEvent (TempAnomaly, PrecipAnomaly, BaselinePeriod)
- [ ] 3.3 Create `ClimateAnomalyHandler` (IDomainHandler, Triggers = {Weather})
- [ ] 3.4 Handler reads cached baseline + current Weather forecast, computes anomalies
- [ ] 3.5 Returns empty if baseline not yet available
- [ ] 3.6 Register as IDomainHandler with ConfigKey "ClimateAnomaly"
- [ ] 3.7 Tests: warm anomaly, dry anomaly, no baseline returns empty

## 4. Validation

- [ ] 4.1 `dotnet build Njord.slnx`
- [ ] 4.2 All test projects pass
