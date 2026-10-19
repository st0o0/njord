## 1. OpenMeteo AQ client

- [x] 1.1 Add AirQualityFetchResult record to Njord.OpenMeteo
- [x] 1.2 Add FetchAirQualityAsync to IOpenMeteoClient and OpenMeteoClient
- [x] 1.3 Build request URL for air-quality-api.open-meteo.com
- [x] 1.4 Tests for URL building and response parsing

## 2. AQ Mappings

- [x] 2.1 Add OpenMeteoAirQualityMappings (response → DomainForecast<AirQualityPoint>)
- [x] 2.2 Update AirQualityPoint if needed (add EuropeanAqi, NitrogenDioxide)
- [x] 2.3 Tests for mapping

## 3. Gateway and PollWorker

- [x] 3.1 Add DomainKind.AirQuality route in OpenMeteoGateway
- [x] 3.2 Add DomainKind.AirQuality to PollWorker EnabledDomains
- [x] 3.3 Tests for gateway routing

## 4. AQ Enrichments

- [x] 4.1 Add AirQualityAlertInput and AirQualityAlertProjector
- [x] 4.2 Add PollenAlert rule
- [x] 4.3 Add Pm25Alert rule
- [x] 4.4 Register AQ ProjectionGroup in DI
- [x] 4.5 Tests for alert rules

## 5. Validation

- [x] 5.1 dotnet build Njord.slnx
- [x] 5.2 All affected test projects pass
