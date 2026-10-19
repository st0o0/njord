## 1. OpenMeteo AQ client

- [ ] 1.1 Add AirQualityFetchResult record to Njord.OpenMeteo
- [ ] 1.2 Add FetchAirQualityAsync to IOpenMeteoClient and OpenMeteoClient
- [ ] 1.3 Build request URL for air-quality-api.open-meteo.com
- [ ] 1.4 Tests for URL building and response parsing

## 2. AQ Mappings

- [ ] 2.1 Add OpenMeteoAirQualityMappings (response → DomainForecast<AirQualityPoint>)
- [ ] 2.2 Update AirQualityPoint if needed (add EuropeanAqi, NitrogenDioxide)
- [ ] 2.3 Tests for mapping

## 3. Gateway and PollWorker

- [ ] 3.1 Add DomainKind.AirQuality route in OpenMeteoGateway
- [ ] 3.2 Add DomainKind.AirQuality to PollWorker EnabledDomains
- [ ] 3.3 Tests for gateway routing

## 4. AQ Enrichments

- [ ] 4.1 Add AirQualityAlertInput and AirQualityAlertProjector
- [ ] 4.2 Add PollenAlert rule
- [ ] 4.3 Add Pm25Alert rule
- [ ] 4.4 Register AQ ProjectionGroup in DI
- [ ] 4.5 Tests for alert rules

## 5. Validation

- [ ] 5.1 dotnet build Njord.slnx
- [ ] 5.2 All affected test projects pass
