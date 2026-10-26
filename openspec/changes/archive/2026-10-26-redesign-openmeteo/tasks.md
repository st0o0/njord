## 1. FetchPlan record and resolution

- [x] 1.1 Create `FetchPlan` record in `src/Njord.Core/Domain/FetchPlan.cs` exposing `Domains` (`IReadOnlySet<DomainKind>`), `Requirements` (`DomainRequirements` with base params applied), `Models` (`EffectiveWeatherModels`), and `WeatherCallWeight` (`int`). Add `BuildFetchPlan` static method to `src/Njord.Core/Domain/FeatureResolution.cs` that computes all four values from `IEnumerable<IFeatureRule>` and the relevant options in a single pass. Log ignored weather models during construction, not in a DI lambda. Tests in `src/Njord.Core.Tests/Domain/` covering the scenarios: all derived values from one computation, no enabled features, unknown model logging.

- [x] 1.2 Replace the three factory lambdas in `src/Njord/Configuration/DomainSetupContainer.cs` with one `FetchPlan` singleton registration via `BuildFetchPlan`. Remove the `IReadOnlySet<DomainKind>`, `EffectiveWeatherModels` and `ResolvedRequirements` singleton registrations. Update `src/Njord.OpenMeteo/Configuration/OpenMeteoSetupContainer.cs` to pass `FetchPlan` to clients instead of `ResolvedRequirements`.

- [x] 1.3 Update `src/Njord.Polling/Configuration/BudgetGuard.cs` to take `FetchPlan` instead of assembling from `ResolvedRequirements`, `WeatherOptions`, `PollingOptions`, `FloodOptions` and a separate `weatherModelCount`. Update `src/Njord.Polling/Configuration/PollingOptionsValidator.cs` similarly. Update tests in `src/Njord.Polling.Tests/BudgetGuardSpec.cs`.

- [x] 1.4 Remove the `ResolvedRequirements` wrapper type from `src/Njord.Core/`. Update all consumers (`src/Njord.OpenMeteo/WeatherClient.cs`, `src/Njord.OpenMeteo/AirQualityClient.cs`, `src/Njord.OpenMeteo/FloodClient.cs`, `src/Njord.OpenMeteo/MarineClient.cs`, `src/Njord.OpenMeteo/SeasonalClient.cs`, `src/Njord.OpenMeteo/SatelliteRadiationClient.cs`) to extract per-domain requirements from `FetchPlan`. Update `src/Njord.Grpc/` consumers if any.

- [x] 1.5 Verify phase 1: build and run all test suites (see Validation section). Commit.

## 2. API variable enums and param mapping

- [x] 2.1 Create per-domain API variable enums in `src/Njord.OpenMeteo/`: `WeatherApiVariable.cs`, `AirQualityApiVariable.cs`, `FloodApiVariable.cs`, `MarineApiVariable.cs`, `SeasonalApiVariable.cs`, `SatelliteApiVariable.cs`. Each member carries `[JsonStringEnumMemberName("api_name")]`. Create a shared `ApiVariableNameCache.cs` (or per-enum static class) that builds a `FrozenDictionary<TEnum, string>` from the attributes once, exposing `ToApiName()` extension methods.

- [x] 2.2 Create per-domain mapping extensions (`WeatherApiVariableMappingExtensions.cs`, etc.) with explicit switch `ToApiVariable(this WeatherParam p)` mapping each domain flags member to the corresponding API variable enum member (default throws `ArgumentOutOfRangeException`). Add `ToApiVariables(this WeatherParam flags)` that iterates set flags and maps each. Add `ToApiNames()` that chains through `ToApiName()`.

- [x] 2.3 Update the six client files to use the new two-step mapping: `FetchPlan.Requirements.Weather.ToApiVariables().Select(v => v.ToApiName())` instead of `ResolvedRequirements.All<Weather>.ToApiNames()`. Remove the six `src/Njord.OpenMeteo/Mapping/*ParamsMappingExtensions.cs` files (`WeatherParamsMappingExtensions`, `AirQualityParamsMappingExtensions`, `FloodParamsMappingExtensions`, `MarineParamsMappingExtensions`, `SeasonalParamsMappingExtensions`, `SatelliteParamsMappingExtensions`).

- [x] 2.4 Create completeness tests in `src/Njord.OpenMeteo.Tests/`: one `[Theory]` per domain asserting every domain flags member has a corresponding API variable enum member and an entry in the frozen dictionary. Update `src/Njord.OpenMeteo.Tests/Mapping/ParamsMappingsSpec.cs` to test the new two-step mapping instead of the old switch-to-string mapping.

- [x] 2.5 Verify phase 2: build and run all test suites. Commit.

## 3. Converter fix and client query constants

- [x] 3.1 In `src/Njord.OpenMeteo/WeatherResponseConverter.cs`, replace the reflected `BaseNames` static field (which reads `[JsonPropertyName]` via reflection) with a hand-written `FrozenSet<string>` constant listing the ~30 API variable base names. Add a test in `src/Njord.OpenMeteo.Tests/WeatherModelResponseSpec.cs` asserting every `[JsonPropertyName]` on `WeatherHourlyData` constructor parameters appears in the `FrozenSet`.

- [x] 3.2 Extract named constants in `src/Njord.OpenMeteo/ClientRequest.cs` for query keys (`hourly`, `daily`, `monthly`, `models`, `timeformat`, `timezone`, `latitude`, `longitude`) and fixed values (`unixtime`, `UTC`). Extract endpoint path constants (one per domain). Update all six clients and `ClientRequest` to use the constants instead of inline string literals.

- [x] 3.3 Verify phase 3: build and run all test suites. Commit.

## 4. Gateway rewrite (stream-based dispatch)

- [x] 4.1 Rewrite `src/Njord.OpenMeteo/OpenMeteoGateway.cs` to use stream-based dispatch: `Source.Queue<(FetchDomain, IActorRef)>` materialized in `PreStart`, `MapAsync(1, ...)` for budget gate (`TryAcquire`, rejected requests reply inline with `FetchDomainRejected`), `SelectAsyncUnordered(maxConcurrent, ...)` for client fetch, internal branch offering `DomainDataReceived` to the pipeline queue on `FetchSuccess`, and `Sink.ActorRef(Self)` delivering results for reply routing. The actor body handles lifecycle (PreStart materializes the stream + resolves pipeline SinkRef), queue offering on `FetchDomain`, and reply routing from stream results. Target ~100 LOC.

- [x] 4.2 Update gateway tests: rewrite `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayRoutingSpec.cs` for stream-based dispatch (budget rejection in stream, bounded concurrency, correct sender routing). Rewrite `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachSpec.cs` for the new pipeline attachment flow. Ensure `src/Njord.OpenMeteo.Tests/GatewayBudgetGateSpec.cs` still passes (budget gate itself unchanged).

- [x] 4.3 Remove the PipeTo-based fetch logic and inline telemetry from the old gateway. Ensure the `FetchDomain`/`FetchDomainCompleted`/`FetchDomainFailed`/`FetchDomainRejected` contract is preserved. Pending request tracking uses monotonic ids as before.

- [x] 4.4 Verify phase 4: build and run all test suites, including `src/Njord.Pipeline.Tests/` and `src/Njord.IntegrationTests/`. Commit.

## 5. Project restructuring

- [x] 5.1 Create domain folders under `src/Njord.OpenMeteo/`: `Weather/`, `AirQuality/`, `Flood/`, `Marine/`, `Seasonal/`, `Satellite/`. Move per-domain files (client, API variable enum, response DTO, response mapping extensions, and `WeatherResponseConverter` for Weather) into their domain folder. Update namespaces to `Njord.OpenMeteo.<Domain>`.

- [x] 5.2 Create shared folders: `Gateway/` (move `OpenMeteoGateway.cs`), `Budget/` (move `GatewayBudgetGate.cs`, `IGatewayBudgetGate.cs`, `IBudgetProvider.cs`, `OptionsBudgetProvider.cs`, `BudgetRate.cs`), `Http/` (move `ClientRequest.cs`, `HttpMetricsHandler.cs`, `OpenMeteoJsonContext.cs`). Keep `Configuration/` as-is. Keep `Mapping/` for shared utilities (`DomainKindMappingExtensions.cs`, `ForecastTrimmingExtensions.cs`, `SeriesAccess.cs`). Keep `Telemetry.cs` and `AssemblyMarker.cs` at the project root.

- [x] 5.3 Update all `using` directives across `src/Njord.OpenMeteo/`, `src/Njord.OpenMeteo.Tests/`, and any other projects referencing moved types. Run `dotnet format` to fix ordering. Ensure no domain-specific files remain at the project root.

- [x] 5.4 Verify phase 5: build and run all test suites. Commit.

## 6. Documentation update

- [x] 6.1 Update `AGENTS.md` to clarify the enum mapping convention: "explicit switch for enum-to-enum mappings (ToProto, ToDomain, ToApiVariable); API variable string names use `[JsonStringEnumMemberName]` with a cached `FrozenDictionary`, outside the switch convention." Update the `Njord.OpenMeteo` section in the solution structure to reflect the new folder layout.

## Validation

All commands run from `src/`:

```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```
