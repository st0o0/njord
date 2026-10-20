## 1. Contract
- [x] 1.1 Specs first, then `IDomainClient` and `FetchResult` in `src/Njord.Core/Domain`
- [x] 1.2 Settle gateway dependencies (location lookup, `TimeProvider`, budget gate construction) and record the decision in design.md

## 2. Thick clients (TDD with a stub HttpMessageHandler)
- [x] 2.1 `WeatherClient`: URL via `QueryHelpers.AddQueryString`, mapping, trimming, errors, telemetry, `CallWeight`
- [x] 2.2 `AirQualityClient`, `FloodClient`, `MarineClient`
- [x] 2.3 `SeasonalClient`, `SatelliteRadiationClient`
- [x] 2.4 Delete `OpenMeteoRequestBuilder.cs` and the per-domain `I*Client.cs` interfaces; migrate the URL specs into client specs

## 3. Gateway
- [x] 3.1 Rewrite `OpenMeteoGateway` thin (client dictionary, gate, timeouts); remove per-domain fields and the `switch`
- [x] 3.2 Update the OpenMeteo DI registration (clients as `IDomainClient`, dictionary singleton)
- [x] 3.3 Rework gateway specs to fake `IDomainClient`; update fakes in `src/Njord.Tests.Shared` and `src/Njord.IntegrationTests`

## 4. Verify
- [x] 4.1 `dotnet build`, `dotnet format Njord.slnx`, run all test projects sequentially
- [ ] 4.2 `openspec validate --all --no-interactive`; commit `refactor!: rewrite gateway with thick clients (FunkArr pattern)`; archive
