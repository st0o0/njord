## 1. Base parameters (F1, F2)

- [x] 1.1 Add `FeatureResolution.WithBaseParams(DomainRequirements)` in `src/Njord.Core/Domain/FeatureResolution.cs`: adds `WeatherParams.WeatherCode` when any weather flag is set and `AirQualityParams.Pm10` when any air-quality flag is set; leaves domains without flags untouched
- [x] 1.2 Specs in `src/Njord.Core.Tests/Domain/FeatureResolutionSpec.cs`: Frost alone gets `WeatherCode`; Pm25 alone gets `Pm10`; requirements without air-quality flags stay without air-quality flags (no domain added)
- [x] 1.3 Use it in `src/Njord/Configuration/DomainSetupContainer.cs`: `new ResolvedRequirements(FeatureResolution.WithBaseParams(FeatureResolution.RequiredParams(rules)))`
- [x] 1.4 Spec in `src/Njord.Polling.Tests/BudgetGuardSpec.cs`: 10 required weather variables plus the base field are weighed as 11 variables (2 calls per cycle)
- [x] 1.5 Spec in `src/Njord.Pipeline.Tests/Stream/FeatureEngineSpec.cs` (or the existing engine spec file): a weather forecast without `weather_code` still dispatches a rule whose own required fields are present

## 2. Failure backoff (F4)

- [x] 2.1 Add trailing `int ConsecutiveFailures = 0` to `DomainPolled` and `PersistedDomainState` in `src/Njord.Persistence/Events/Polling/`
- [x] 2.2 Add `ConsecutiveFailures` (default 0) to `DomainPollState`; `Apply(DomainPolled)` in `PollWorkerStateExtensions.cs` copies it; `PollWorkerPersistenceMappingExtensions` maps it both ways; update `src/Njord.Polling.Tests/PollWorkerEventMappingSpec.cs` and add a recovery spec that an old record (no count) recovers as 0
- [x] 2.3 Add `internal static class FailureBackoff` in `src/Njord.Polling/FailureBackoff.cs`: `Delay(int consecutiveFailures, TimeSpan pollInterval)` = `min(60 s * 2^(n-1), pollInterval)` (overflow-safe), plus the 5 min floor for rate limit/budget
- [x] 2.4 `src/Njord.Polling.Tests/FailureBackoffSpec.cs`: 1 → 60 s, 2 → 2 min, 3 → 4 min, 10 with 60 min interval → 60 min, rate limit after 1 failure → 5 min
- [x] 2.5 `PollWorker` takes `IOptions<PollingOptions>`; `OnDomainFetchResult` computes the new count (failure: +1, Changed/Unchanged: 0, PipelineUnavailable: unchanged, 5 s retry) and persists it with the delay from `FailureBackoff`; replace `FailureDelay`/`DefaultRetry` usage for failures; update the construction sites (`PollingRegistrationExtensions.cs`, `PollWorkerGatewayConnectionSpec.cs`, `PollWorkerTriggerSpec.cs`, any other `Props` in tests) with `Options.Create(new PollingOptions())`
- [x] 2.6 Actor specs in `src/Njord.Polling.Tests/PollWorkerFailureSpec.cs` (`TestTimeouts.Hosted`): third consecutive failure persists a 4 min delay; fail ×3, success, fail persists 60 s; Marine failures leave the Weather schedule unchanged; recovery after four failures then one more failure persists 16 min
- [x] 2.7 Budget estimate in the PR notes: an always-failing domain costs at most 24 calls/day/location after the first hour (was 1,440)

## 3. SeasonalOutlook on seasonal data alone (F6)

- [x] 3.1 Replace `SeasonalWeatherInput` with `SeasonalInput(DomainForecast<SeasonalPoint> Seasonal)` in `src/Njord.Core/Domain/FeatureInputs.cs`; `SeasonalOutlook.Project` needs only the seasonal forecast
- [x] 3.2 `src/Njord.Features.Tests/SeasonalOutlook/SeasonalOutlookSpec.cs`: emits with seasonal data and no weather data; existing conversion/past-month/no-data scenarios still pass

## 4. Seasonal domain data on gRPC (F6)

- [x] 4.1 `protos/njord/v2/weather.proto`: add `SeasonalSample`, `SeasonalData` and `SeasonalData seasonal = 16;` to the `DomainDataUpdate` oneof
- [x] 4.2 Map `DomainForecast<SeasonalPoint>` in `src/Njord.Grpc/Mapping/DomainDataProtoMappingExtensions.cs`; spec `src/Njord.Grpc.Tests/SeasonalSampleMappingsSpec.cs` (time and the three optional values, null stays unset)
- [x] 4.3 `GrpcConsumerState` keeps the latest seasonal forecast (`GrpcConsumerStateExtensions.Apply`); check `src/Njord.Pipeline/Stream/PipelineEventMappingExtensions.cs` carries seasonal domain data; specs in `GrpcConsumerStateSpec.cs` and `WeatherGrpcServiceSpec.cs` (`GetDomainData` for Seasonal returns SeasonalData; filtered stream delivers it)

## 5. Gateway uses the resolved location (F5)

- [x] 5.1 In `src/Njord.OpenMeteo/OpenMeteoGateway.cs` every `FetchDomainCompleted/Failed/Rejected` reply and log line uses `PendingDomainRequest.Resolved`
- [x] 5.2 Spec in `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayRoutingSpec.cs`: a `FetchDomain` with an unresolved `Location(name, 0, 0)` gets a reply carrying the configured coordinates

## 6. Docs (F3, budget)

- [x] 6.1 `docs/data/features.json`: `Weather:Visibility` and `Weather:Uv` note that the default ensemble model delivers neither field; same note on the weather models docs page
- [x] 6.2 `docs/.vitepress/theme/builder/types.ts`: weather variable count includes the base field `weather_code`
- [x] 6.3 `AGENTS.md`: mention base parameters and the failure backoff under Decisions; update test counts

## 7. Validation

- [x] 7.1 `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes` (from `src/`)
- [x] 7.2 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`
- [x] 7.3 `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj`
- [x] 7.4 `dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj`
- [x] 7.5 `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj`
- [x] 7.6 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`
- [x] 7.7 `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`
- [x] 7.8 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`
- [x] 7.9 `dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj` and `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
