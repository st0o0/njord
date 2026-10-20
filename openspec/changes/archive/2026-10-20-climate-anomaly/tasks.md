## 1. Climate Client

- [x] 1.1 Create `ClimateResponse` + `ClimateDailyData` typed response models in Njord.OpenMeteo
- [x] 1.2 Create `IClimateClient` + `ClimateClient` targeting climate-api.open-meteo.com
- [x] 1.3 Add to OpenMeteoJsonContext and DI registration
- [x] 1.4 Tests for URL building and response parsing

## 2. Climate Baseline Service

- [x] 2.1 Create `MonthlyBaseline` record (MeanTemp, MeanPrecip, MeanSunshine, Month)
- [x] 2.2 Create `ClimateBaselineService` (BackgroundService) that fetches 30 years per location, aggregates to 12 monthly averages
- [x] 2.3 Cache as `IReadOnlyDictionary<string, MonthlyBaseline[]>` singleton
- [x] 2.4 Daily refresh timer
- [x] 2.5 Tests for aggregation logic (given daily data, compute monthly means)

## 3. Climate Anomaly Handler

- [x] 3.1 Add `ClimateAnomaly` bool to EnrichmentOptions (default false)
- [x] 3.2 Create `ClimateAnomalyComputed` PipelineEvent (TempAnomaly, PrecipAnomaly, BaselinePeriod)
- [x] 3.3 Create `ClimateAnomalyHandler` (IDomainHandler, Triggers = {Weather})
- [x] 3.4 Handler reads cached baseline + current Weather forecast, computes anomalies
- [x] 3.5 Returns empty if baseline not yet available
- [x] 3.6 Register as IDomainHandler with ConfigKey "ClimateAnomaly"
- [x] 3.7 Tests: warm anomaly, dry anomaly, no baseline returns empty

## 4. Validation

- [x] 4.1 `dotnet build Njord.slnx`
- [x] 4.2 All test projects pass
