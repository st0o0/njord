All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD (red first); xUnit v3, `Spec` suffix, sealed classes, `TestContext.Current.CancellationToken`, `FakeTimeProvider`. Load `njord-enrichment-feature` before section 3 and `njord-actor-spec` before writing any actor/handler spec (hosted-service specs may use `NjordActorSpec` with Akka.Hosting TestKit; never create an `ActorSystem` by hand). Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Seasonal Client

- [ ] 1.1 Verify the Open-Meteo Seasonal API (variables, anomaly variants, units, model names, member count); record the outcome in design D2 and pick the baseline option (API anomalies or Archive fallback)
- [ ] 1.2 Create `SeasonalSixHourlyData` and `SeasonalResponse` in `src/Njord.OpenMeteo/SeasonalResponse.cs`; add to `OpenMeteoJsonContext`
- [ ] 1.3 Create `ISeasonalClient` + `SeasonalClient` in `src/Njord.OpenMeteo/` using `OpenMeteoRequestBuilder` (thin: variables passed in, no internal list), base address `https://seasonal-api.open-meteo.com/`
- [ ] 1.4 Register in `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` via `AddTypedClient<ISeasonalClient, SeasonalClient>` (metrics handler + standard resilience)
- [ ] 1.5 Tests in `src/Njord.OpenMeteo.Tests`: URL building, response parsing
- [ ] 1.6 Validate: `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`

## 2. Seasonal Outlook Service

- [ ] 2.1 Create `MonthlyOutlook` and `PipelineEvent.SeasonalOutlookComputed` in `src/Njord.Messages/Pipeline/` (Akka-free)
- [ ] 2.2 Create `ISeasonalOutlookStore` + `SeasonalOutlookStore` in `src/Njord.Core/Domain/` (thread-safe, per location)
- [ ] 2.3 Create `SeasonalResponseMappings` in `src/Njord.OpenMeteo/Mapping/`: 6-hourly to monthly anomalies, partial-month drop, spread, confidence (High below 1.5, Medium below 3.0, else Low)
- [ ] 2.4 Create `SeasonalOutlookService` (`BackgroundService`) in `src/Njord.OpenMeteo/`: toggle-gated, daily per location, jittered first run, 15 min retry keeping the previous outlook, skips locations without coordinates, all timing via `TimeProvider`; confirm the coordinate source and whether budget accounting needs a hook
- [ ] 2.5 Register the service in `OpenMeteoSetupContainer` and the store as a singleton
- [ ] 2.6 Tests: aggregation, anomaly values, confidence levels, disabled toggle, retry keeps previous, `FakeTimeProvider`-driven loop (no `Thread.Sleep`)
- [ ] 2.7 Validate: `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`, `Njord.Messages.Tests`, `Njord.Core.Tests`

## 3. Seasonal Outlook Handler

- [ ] 3.1 Add `SeasonalOutlook` (default false) to `src/Njord.Core/Configuration/EnrichmentOptions.cs` (and `Njord.Messages/Pipeline/EnrichmentNames.cs` if it lists toggles); `EnrichmentConfigKeySpec` must pass
- [ ] 3.2 Create `SeasonalOutlookHandler` in `src/Njord.Weather/Lifestyle/`: `ConfigKey "SeasonalOutlook"`, `Requirements = new(Weather: WeatherParam.Temperature)`, reads the store, emits next 3 months, empty list when the store is empty
- [ ] 3.3 Register `services.AddSingleton<IDomainHandler, SeasonalOutlookHandler>()` in `src/Njord.Weather/ServiceCollectionExtensions.cs`
- [ ] 3.4 Tests in `src/Njord.Weather.Tests`: outlook emitted, empty store, requirements pinned, 3-month window from the current month
- [ ] 3.5 Validate: `dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj`, `Njord.Architecture.Tests`, `Njord.Core.Tests`

## 4. Wire + Consumer

- [ ] 4.1 Add a `LifestyleKind` value and `SeasonalOutlookComputed` arm to `src/Njord.Messages/Pipeline/LifestyleMappings.cs` (explicit switch, flatten months to the string map)
- [ ] 4.2 Verify gRPC (`src/Njord.Grpc/Mapping/EnrichmentEventMappings.cs`) and MQTT (`src/Njord.Mqtt/Mapping/PipelineEventMqttMappings.cs`) handle it through the existing lifestyle path; add arms only if the switches require it
- [ ] 4.3 Tests: wire format specs in Messages, Grpc and Mqtt tests
- [ ] 4.4 Validate: `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`, `Njord.Grpc.Tests`, `Njord.Mqtt.Tests`

## 5. Config Builder + Docs

- [ ] 5.1 Update `docs/.vitepress/theme/builder` (`types.ts`, `serializer.ts`), `docs/data/enrichment.json` and `docker-compose.example.yml` for the new toggle
- [ ] 5.2 Update `AGENTS.md` (toggle count 21, new service, new handler) and `src/Njord/appsettings.Example.json`

## 6. ha-njord

- [ ] 6.1 Add seasonal outlook sensor entity showing next month's temp/precip anomaly
- [ ] 6.2 Add `seasonal_outlook` enrichment toggle to the config flow
- [ ] 6.3 Tests (run in a python:3.12-slim Docker container on Windows)

## 7. Validation

- [ ] 7.1 `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
- [ ] 7.2 All affected test projects pass sequentially, plus `dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj`
- [ ] 7.3 `openspec validate --all --no-interactive` (repo root)
