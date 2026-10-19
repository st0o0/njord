## 1. OpenMeteo Flood client

- [x] 1.1 Add FloodFetchResult record to Njord.OpenMeteo
- [x] 1.2 Add FetchFloodAsync to IOpenMeteoClient and OpenMeteoClient
- [x] 1.3 Build request URL for flood-api.open-meteo.com (daily resolution, 92 days)
- [x] 1.4 Tests for URL building and response parsing

## 2. Flood Mappings

- [x] 2.1 Add OpenMeteoFloodMappings (response → DomainForecast<FloodPoint> with TimeResolution.Daily)
- [x] 2.2 Update FloodPoint with full API fields (DischargeMean, DischargeMedian, DischargeP25, DischargeP75, DischargeMin, DischargeMax)
- [x] 2.3 Tests for mapping

## 3. Gateway and PollWorker

- [x] 3.1 Add DomainKind.Flood route in OpenMeteoGateway
- [x] 3.2 Add DomainKind.Flood to PollWorker EnabledDomains
- [x] 3.3 Tests for gateway routing

## 4. Flood Enrichments

- [x] 4.1 Add FloodRiskInput and FloodRiskProjector
- [x] 4.2 Add FloodRiskAlert rule (discharge vs percentiles)
- [x] 4.3 Add FloodRainInput and FloodRainProjector (cross-domain: Flood × Weather)
- [x] 4.4 Add FloodRainAlert rule
- [x] 4.5 Register Flood ProjectionGroups in DI
- [x] 4.6 Tests for all enrichment rules

## 5. Validation

- [x] 5.1 dotnet build Njord.slnx
- [x] 5.2 All affected test projects pass
