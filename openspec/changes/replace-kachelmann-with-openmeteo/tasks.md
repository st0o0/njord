# Tasks — replace-kachelmann-with-openmeteo

## 1. Domain: apparentTemperature joins the closed parameter set

- [x] 1.1 Extend `src/Njord.Tests/Domain/WeatherParameterSpec.cs` (TDD first):
      `ApparentTemperature` is part of the closed set with unit `°C`; then add
      it to `src/Njord/Domain/WeatherParameter.cs`
- [x] 1.2 Extend `src/Njord/Domain/ForecastPoint.cs` with the nullable
      apparent-temperature value and update
      `src/Njord.Tests/Domain/ForecastSeriesSpec.cs` /
      `src/Njord.Tests/Domain/ModelForecastSpec.cs` where points are built
- [x] 1.3 Update model-id examples in
      `src/Njord.Tests/Domain/WeatherModelSpec.cs` to Open-Meteo ids
      (`icon_d2`, `ecmwf_ifs025`) — behavior unchanged (free-form ids)

## 2. Configuration: plans and API key out, free-tier default in

- [x] 2.1 Rewrite budget resolution tests (replace
      `src/Njord.Tests/Configuration/PlanBudgetsSpec.cs`): no configured
      budget resolves to 300,000/month + 600/min; explicit override wins;
      then delete `src/Njord/Configuration/NjordPlan.cs` and
      `src/Njord/Configuration/PlanBudgets.cs` and put the default on
      `src/Njord/Configuration/RequestBudget.cs` /
      `src/Njord/Configuration/NjordOptions.cs` (drop `Plan` and `ApiKey`
      properties)
- [x] 2.2 Update `src/Njord/Configuration/NjordOptionsValidator.cs` +
      `src/Njord.Tests/Configuration/NjordOptionsValidatorSpec.cs`: remove
      API-key validation; keep 80 % monthly projection guard with new
      scenario numbers (1 loc × 8 models × 60 min ≈ 5,760 passes; 2 loc ×
      8 models vs. 10,000 override fails at 8,000 guard)
- [x] 2.3 Update `src/Njord/appsettings.json`: remove plan/key, set default
      models `icon_d2, icon_eu, icon_global, ecmwf_ifs025, gfs_seamless,
      ukmo_global_deterministic_10km, meteoswiss_icon_ch1,
      meteoswiss_icon_ch2` (all verified for the Lucerne default location)

## 3. Ingest: OpenMeteoClient replaces KachelmannClient

- [x] 3.1 Create `src/Njord/Ingest/OpenMeteoDtos.cs` +
      `src/Njord/Ingest/OpenMeteoJsonContext.cs` (flat arrays payload,
      `hourly_units`, error payload `{"error":true,"reason":…}`); TDD with
      `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` using verified sample
      payloads from design.md
- [x] 3.2 Request building in `src/Njord/Ingest/OpenMeteoClient.cs`: exact
      9-variable `hourly` list, `wind_speed_unit=ms`, `timeformat=unixtime`,
      `forecast_days=4`, no auth header (spec: call weight 1.0)
- [x] 3.3 Mapping to `ModelForecast`: unixtime → `DateTimeOffset`, nulls →
      missing values, all-null tail trimmed, `hourly_units` verification →
      `MalformedPayload` on drift
- [x] 3.4 Failure taxonomy in `src/Njord/Ingest/FetchOutcome.cs`: remove
      `AuthFailed`; 429 → `RateLimited`, 400 + error payload →
      `ModelUnavailable` (with model id + API reason), network →
      `Transport`, one attempt each
- [x] 3.5 Replace `src/Njord/Ingest/IKachelmannClient.cs` with
      `IOpenMeteoClient`, update
      `src/Njord/Ingest/IngestServiceCollectionExtensions.cs` (base address
      `https://api.open-meteo.com`, no key plumbing); delete
      `KachelmannClient.cs`, `KachelmannDtos.cs`, `KachelmannJsonContext.cs`
      and `src/Njord.Tests/Ingest/KachelmannClientSpec.cs`

## 4. Pipeline and host wiring

- [x] 4.1 Rename client references in `src/Njord/Pipeline/PollPipeline.cs` /
      `src/Njord/Pipeline/PipelineGuardianActor.cs`; confirm
      `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` and
      `PipelineGuardianActorSpec.cs` pass with only mechanical renames (no
      requirement change — spec deliberately untouched)
- [x] 4.2 Update `src/Njord/Program.cs`: drop API-key configuration path

## 5. Smoke test and docs

- [x] 5.1 Replace `src/Njord.Tests/Ingest/KachelmannSmokeSpec.cs` with
      `OpenMeteoSmokeSpec.cs`: still env-gated (network opt-in) but keyless;
      asserts one real fetch for `icon_d2` at a German location succeeds
- [x] 5.2 Update `CLAUDE.md`: replace the Kachelmann API facts section with
      the verified Open-Meteo facts (endpoint, soft limits, call weighting,
      single-vs-multi-model response shape, coverage behavior, unit params),
      drop the Steadman note and plan-preset decision

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx` (from `src/`) — clean build
- [x] 6.2 `dotnet run --project Njord.Tests/Njord.Tests.csproj` (from `src/`)
      — full suite green (34 tests, 1 gated smoke skipped; smoke also run
      live via `NJORD_SMOKE_TESTS=1` — pass)
- [x] 6.3 `dotnet slopwatch` (from repo root, where the baseline lives) —
      0 issues
- [x] 6.4 One real poll cycle without any key: `dotnet run` from `src/Njord/`
      (content root must contain appsettings.json) — cycle summary logged
      8 received, 0 failed, 0 unanswered for location `home`
