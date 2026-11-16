## 1. ContentHash method

- [x] 1.1 Add `int ContentHash()` to `IDomainForecast` interface
- [x] 1.2 Implement `ContentHash()` on `DomainForecast<T>`: hash Domain + Resolution + all Points via HashCode, exclude ModelRun
- [x] 1.3 Add unit tests in `Njord.Core.Tests`: identical points produce same hash, different points produce different hash, different ModelRun produces same hash

## 2. Gateway integration

- [x] 2.1 Change `OpenMeteoGateway` to call `ok.Forecast.ContentHash()` instead of `ok.Forecast.GetHashCode()`
- [x] 2.2 Update Gateway routing tests if they assert on hash values

## 3. ModelRun fix

- [x] 3.1 Change all 6 domain clients to derive ModelRun from the first point's ValidFrom instead of `now`: ForecastClient, EnsembleClient, AirQualityClient, MarineClient, FloodClient, SeasonalClient
- [x] 3.2 Update OpenMeteoClientTests URI/mapping tests if they assert on ModelRun values

## 4. Verification

- [x] 4.1 Run all test projects, verify 0 failures
- [x] 4.2 Start E2E stack, trigger 2 polls with identical data, verify GetStatus shows Unchanged (hash match) via per-domain states
