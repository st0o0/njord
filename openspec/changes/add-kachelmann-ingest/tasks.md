# Tasks: add-kachelmann-ingest

## 1. Packages & bootstrap

- [x] 1.1 From `src/`, add service packages via CPM: `dotnet add Njord/Njord.csproj package Akka.Hosting`, `Akka.Streams`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Http` (versions land in `src/Directory.Packages.props` only)
- [x] 1.2 From `src/`, add test packages: reference the already-pinned `Microsoft.Extensions.TimeProvider.Testing` (Akka.Hosting.TestKit was added, then removed again — it targets xunit v2 and the stream tests run against a plain `ActorSystem` instead)
- [x] 1.3 Bootstrap `src/Njord/Program.cs`: generic host, bind `NjordOptions` from section `Njord`, `AddAkka` with a placeholder guardian, `ValidateOnStart` wiring; add `src/Njord/appsettings.json` with a commented example config (no API key)

## 2. service-configuration

- [x] 2.1 `src/Njord/Configuration/NjordOptions.cs`: options record (plan, budget override, locations, models, poll interval default 60 min, API key) + `src/Njord/Configuration/PlanBudgets.cs`: preset table (Hobby = 20,000/month + 60/min verified; other plans conservative placeholders documented as unverified; `Custom` requires override) and resolved-budget accessor. Tests: `src/Njord.Tests/Configuration/PlanBudgetsSpec.cs` (sealed, `[Fact(Timeout = 5000)]`, BDD names) — hobby preset resolves verified limits, override supersedes preset, custom without override is invalid
- [x] 2.2 `src/Njord/Configuration/NjordOptionsValidator.cs` (`IValidateOptions<NjordOptions>`): monthly projection `locations × models × cycles/month`, fail above 80 % of resolved monthly budget with projection + limit in the message; require ≥1 location, ≥1 non-blank model, non-empty API key. Tests: `src/Njord.Tests/Configuration/NjordOptionsValidatorSpec.cs` — default config passes (≈2,880), 6 locations × 4 models hourly rejected (≈17,280 vs 16,000), empty model list rejected, missing API key rejected, interval defaults to 60 min

## 3. weather-domain

- [x] 3.1 `src/Njord/Domain/WeatherParameter.cs`: closed enum (Temperature, Precipitation, WindSpeed, WindGust, Dewpoint, RelativeHumidity, CloudCover, PressureMsl) + unit metadata (°C, mm, m/s, %, hPa) via exhaustive switch. Tests: `src/Njord.Tests/Domain/WeatherParameterSpec.cs` — every parameter has a unit
- [x] 3.2 `src/Njord/Domain/WeatherModel.cs` (value record wrapping non-blank free-string id) and `src/Njord/Domain/CycleId.cs` (derived from a `DateTimeOffset` tick timestamp). Tests: `src/Njord.Tests/Domain/WeatherModelSpec.cs` — blank id rejected, value equality holds
- [x] 3.3 `src/Njord/Domain/ForecastPoint.cs` (ValidAt + one nullable value per parameter, `Get(WeatherParameter)` accessor), `src/Njord/Domain/ForecastSeries.cs` (normalizes to ascending ValidAt), `src/Njord/Domain/ModelForecast.cs` (model + location + cycle + RetrievedAt + series). Tests: `src/Njord.Tests/Domain/ForecastSeriesSpec.cs` — unordered input normalized, point with missing dewpoint retained; `src/Njord.Tests/Domain/ModelForecastSpec.cs` — identifiers readable

## 4. kachelmann-client

- [x] 4.1 `src/Njord/Ingest/KachelmannDtos.cs` + `src/Njord/Ingest/KachelmannJsonContext.cs` (source-generated STJ context) modeled from the OpenAPI advanced-forecast schema; hand-written fixture `src/Njord.Tests/Ingest/Fixtures/advanced-3h-sample.json` (schema-derived fake data — never a real captured payload, never a key)
- [x] 4.2 `src/Njord/Ingest/FetchOutcome.cs`: `Success(ModelForecast)` / `Failure(FetchFailure)` with reasons AuthFailed, RateLimited, ModelUnavailable, MalformedPayload, Transport
- [x] 4.3 `src/Njord/Ingest/KachelmannClient.cs` (`IKachelmannClient`): typed `IHttpClientFactory` client, base `https://api.kachelmannwetter.com/v02`, `GET /forecast/{lat}/{lon}/advanced/3h?model=<id>` with metric units, `X-API-Key` from options, DTO→`ModelForecast` mapping, exactly one HTTP attempt per call, key never in logs/outcomes; DI registration extension `src/Njord/Ingest/IngestServiceCollectionExtensions.cs`
- [x] 4.4 Tests: `src/Njord.Tests/Ingest/KachelmannClientSpec.cs` with a fake `HttpMessageHandler` — 200+fixture maps to domain incl. +72 h point, 401→AuthFailed, 429→RateLimited, unknown model→ModelUnavailable, invalid JSON→MalformedPayload, network error→Transport after exactly one attempt, no outcome/log contains the key
- [x] 4.5 Env-gated smoke test `src/Njord.Tests/Ingest/KachelmannSmokeSpec.cs`: skipped unless `Njord__ApiKey` is set at runtime; one real `ICON-D2` call asserting parseability (verifies real field names + units parameter — Design open question)

## 5. poll-pipeline

- [x] 5.1 `src/Njord/Pipeline/PollMessages.cs`: `FetchRequest(cycle, location, model)`, `CycleResult(cycle, received, missing)`; cycle id from injected `TimeProvider`. Tests: `src/Njord.Tests/Pipeline/CycleIdSpec.cs` — cycle id derives from FakeTimeProvider tick
- [x] 5.2 `src/Njord/Pipeline/PollPipeline.cs`: graph per design D6 — Tick → cycle id → fan-out locations × models → `Throttle(per-minute budget)` → `SelectAsyncUnordered(4)` → `GroupBy(cycleId)` → `TakeWithin(aggregation window)` → aggregate → `MergeSubstreams`, wrapped in `RestartSource.WithBackoff(5 s, 5 min, 0.2)`
- [x] 5.3 `src/Njord/Pipeline/PipelineGuardianActor.cs`: materializes/owns the stream, logging sink (one summary per cycle: cycle id, per-model outcome, received/missing counts); registered via Akka.Hosting in `Program.cs`
- [x] 5.4 Tests: `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` (Akka.Hosting.TestKit + fake `IKachelmannClient`) — 2 locations × 4 models issues exactly 8 requests per cycle, 3-of-4 success + 1 timeout emits partial `CycleResult` within the window, stage exception does not terminate the test system's host, exactly one summary log per cycle

## 6. Verification & closure

- [ ] 6.1 SWISS1X1 probe (needs the user's key, run manually): `curl -H "X-API-Key: $env:NJORD_PROBE_KEY" "https://api.kachelmannwetter.com/v02/forecast/47.0/8.0/advanced/3h?model=SWISS1X1"` — confirm it is "Super HD" and hobby-plan-accessible; set the default model list accordingly (keep ICON-D2/ECMWF/GFS regardless)
- [x] 6.2 Full build clean: `dotnet build Njord.slnx` from `src/` with zero warnings introduced (NU1902 from Akka's transitive OpenTelemetry.Api 1.10.0 resolved via CPM transitive pin to 1.16.0)
- [x] 6.3 Run `dotnet slopwatch` (local tool) over the change; fix findings (0 issues; baseline initialized at `.slopwatch/baseline.json`)
- [ ] 6.4 Run the service once locally with a real key (env var only) and observe one full poll cycle summary in the logs

## Validation

From `src/`:

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Tests/Njord.Tests.csproj
# touched suites individually, e.g.:
dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Configuration.NjordOptionsValidatorSpec"
dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Ingest.KachelmannClientSpec"
dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Pipeline.PollPipelineSpec"
```
