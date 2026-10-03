## 1. Package Setup

- [x] 1.1 Add `WireMock.Net.Testcontainers` to `src/Directory.Packages.props` and `src/Njord.Tests/Njord.Tests.csproj`
- [x] 1.2 Verify `dotnet build src/Njord.slnx` succeeds with the new dependency

## 2. Unit Tests for Uncovered Value Objects

- [x] 2.1 Create `src/Njord.Tests/Domain/Weather/CycleIdSpec.cs` — equality, timestamp preservation, different timestamps are not equal
- [x] 2.2 Create `src/Njord.Tests/Domain/Weather/TimeAnchorSpec.cs` — `AtHorizon` rounds to full hours, various horizon offsets
- [x] 2.3 Create `src/Njord.Tests/Configuration/RequestBudgetSpec.cs` — default budget values, effective limits computation, budget override
- [x] 2.4 Create `src/Njord.Tests/Pipeline/WeightedTargetSpec.cs` — property access, weight value
- [x] 2.5 Create `src/Njord.Tests/Egress/HorizonProjectionSpec.cs` — builds per-horizon JSON with correct keys, handles missing data points, includes daily entries
- [x] 2.6 Create `src/Njord.Tests/Egress/TopicSlugSpec.cs` — sanitization of special characters, lowercase conversion

## 3. Remove Docker Test Gate

- [x] 3.1 Remove the `Assert.SkipWhen(Environment.GetEnvironmentVariable("NJORD_DOCKER_TESTS") != "1", ...)` from `src/Njord.Tests/Mqtt/MqttEgressIntegrationSpec.cs`

## 4. WireMock Integration Tests for OpenMeteoClient

- [x] 4.1 Create `src/Njord.Tests/Ingest/OpenMeteoClientIntegrationSpec.cs` with a WireMock container fixture (`IAsyncLifetime`). Configure WireMock to serve `openmeteo-icon_eu-96h.json` on `/v1/forecast` with `models=icon_eu` query match
- [x] 4.2 Test: successful fetch through real HTTP — `FetchAsync` against WireMock returns `FetchOutcome.Success` with correct `ModelForecast`
- [x] 4.3 Test: request URL construction — verify WireMock request log shows correct query parameters (`latitude`, `longitude`, `models`, `hourly`, `wind_speed_unit=ms`, `timeformat=unixtime`, `forecast_days=4`)
- [x] 4.4 Test: HTTP 429 rate limiting — configure WireMock to return 429, assert `FetchOutcome.Failure` with `RateLimited`
- [x] 4.5 Test: HTTP 400 model unavailable — configure WireMock to return 400 with error JSON, assert `FetchOutcome.Failure` with `ModelUnavailable`
- [x] 4.6 Test: icon_d2 fixture with horizon trimming — serve `openmeteo-icon_d2-96h.json`, assert 64 points after tail trim

## 5. Full E2E Pipeline Integration Test

- [x] 5.1 Create `src/Njord.Tests/Integration/EndToEndPipelineSpec.cs` — boots WireMock container + Mosquitto container, configures WireMock with fixture responses for 2 models (`icon_eu`, `icon_d2`)
- [x] 5.2 Wire up OpenMeteoClient pointing at WireMock, MqttNetPublisher at Mosquitto — full data path without actor overhead
- [x] 5.3 Trigger one poll cycle and wait for retained MQTT messages on Mosquitto
- [x] 5.4 Assert: correct number of horizon state topics published per model (horizons + forecast days)
- [x] 5.5 Assert: horizon JSON payloads contain expected parameter keys (`temperature`, `wind_speed`, etc.)
- [x] 5.6 Assert: discovery device configs present at `homeassistant/device/njord_<loc>_<model>/config` with correct component count
- [x] 5.7 Assert: availability topic `njord/status` contains "online"

## 6. Validation

- [x] 6.1 Run all tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` — all pass (360 total, 0 failed, 1 skipped/smoke)
- [x] 6.2 Run `dotnet slopwatch` from repo root — 1 pre-existing warning (AppHost NoWarn), no new regressions
