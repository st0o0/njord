## 1. OpenMeteo Marine client

- [ ] 1.1 Add MarineHourlyData + MarineResponse typed response models
- [ ] 1.2 Add IMarineClient + MarineClient
- [ ] 1.3 Add to OpenMeteoJsonContext
- [ ] 1.4 Register HttpClient in OpenMeteoSetupContainer
- [ ] 1.5 Tests for URL building and response parsing

## 2. Mapping and Gateway

- [ ] 2.1 Add MarineResponseMappings (response → DomainForecast<MarinePoint>)
- [ ] 2.2 Verify MarinePoint fields match API variables
- [ ] 2.3 Add DomainKind.Marine route in Gateway
- [ ] 2.4 Add FakeMarineClient to Tests.Shared
- [ ] 2.5 Gateway routing test

## 3. Marine project and enrichment

- [ ] 3.1 Create Njord.Marine project (csproj, reference Core)
- [ ] 3.2 Implement SwimmingConditionsHandler (IDomainHandler)
- [ ] 3.3 Add SwimmingConditions PipelineEvent type
- [ ] 3.4 Add AddMarineDomain DI extension
- [ ] 3.5 Register in host

## 4. Tests

- [ ] 4.1 Create Njord.Marine.Tests project
- [ ] 4.2 SwimmingConditionsHandlerSpec (3 facts)
- [ ] 4.3 Update architecture tests for new project

## 5. Solution and config

- [ ] 5.1 Add projects to Njord.slnx
- [ ] 5.2 Marine is opt-in (not in default EnabledDomains)
- [ ] 5.3 Update docker-compose.example.yml

## 6. Validation

- [ ] 6.1 dotnet build Njord.slnx
- [ ] 6.2 All affected test projects pass
