## 1. WritableConfigProvider

- [x] 1.1 Create `WritableMemoryConfigurationProvider` and `WritableMemoryConfigurationSource` in `src/Njord/Configuration/`. The provider subclasses `MemoryConfigurationProvider`, exposes `Update(key, value)`, `UpdateBatch(dict)`, `Remove(key)` — each calls `OnReload()`. Includes debounced file flush to a configurable path (default `data/config-overrides.json`). On construction, loads existing overrides from file if present.
- [x] 1.2 Register the writable source as last provider in `src/Njord/Configuration/NjordServiceSetup.cs` via `builder.Configuration.Add(source)`. Register the provider instance as a DI singleton.
- [x] 1.3 Write `WritableMemoryConfigurationProviderSpec` in `src/Njord.Tests/Configuration/`. Tests: single update triggers options change, batch update fires once, remove reverts to base value, file flush persists overrides, startup loads existing override file, missing file on startup is no error. Use `[Fact(Timeout = 5000)]`, sealed class, BDD method names.

## 2. NjordOptionsValidator runtime reuse

- [x] 2.1 Extract validation logic from `NjordOptionsValidator.Validate()` in `src/Njord/Configuration/NjordOptionsValidator.cs` into a `static ValidateOptions(NjordOptions)` method returning `ValidateOptionsResult`. The existing `IValidateOptions<T>.Validate` delegates to it.
- [x] 2.2 Write `NjordOptionsValidatorSpec` tests in `src/Njord.Tests/Configuration/` verifying runtime validation (budget guard, model coverage, horizon bounds) returns the same results as startup validation.

## 3. Hot model/location lifecycle — SchedulerActor

- [x] 3.1 Define `AddTargets` and `RemoveTargets` command records and `TargetsAdded`/`TargetsRemoved` persistence event records in `src/Njord/Pipeline/SchedulerActor.cs` (or a separate messages file if conventions require).
- [x] 3.2 Implement `AddTargets` handler: for each new target, create `ModelPollState` in Discovery phase, persist `TargetsAdded`, recompute `_weight`, schedule first poll. Skip already-existing targets.
- [x] 3.3 Implement `RemoveTargets` handler: for each existing target, cancel timer, remove from `_states`, persist `TargetsRemoved`. Skip non-existent targets.
- [x] 3.4 Update `OnRecover` to handle `TargetsAdded` and `TargetsRemoved` events alongside existing `DataChanged` events.
- [x] 3.5 Write `SchedulerActorHotLifecycleSpec` (requires extending TestableSchedulerActor with AddTargets/RemoveTargets support) in `src/Njord.Tests/Pipeline/`. Tests: add new target starts Discovery polling, add duplicate is no-op, remove target stops polling, remove non-existent is no-op, recovery rebuilds state from add+remove events, weight recomputed after add.

## 4. Hot model/location lifecycle — DiscoveryActor

- [x] 4.1 Switch `DiscoveryActor` in `src/Njord/Mqtt/DiscoveryActor.cs` from `IOptions<NjordOptions>` to `IOptionsMonitor<NjordOptions>`, reading `.CurrentValue` in `PublishDiscovery()`.
- [x] 4.2 Add `_publishedDeviceIds` (`HashSet<string>`) tracking published device IDs. Populate on each `PublishDiscovery()` call.
- [x] 4.3 Define `RefreshDiscovery` message. Implement handler: read current options, compute new device set, diff against `_publishedDeviceIds`, publish new devices, tombstone (empty retained message) removed devices.
- [x] 4.4 Write `DiscoveryActorRefreshSpec` in `src/Njord.Tests/Mqtt/`. Tests: refresh after add publishes new discovery payload, refresh after remove publishes tombstone, refresh with no change produces no messages, discovery uses current options not construction-time snapshot.

## 5. Hot model/location lifecycle — EnrichmentActor

- [x] 5.1 In `src/Njord/Enrichment/EnrichmentActor.cs`, replace the captured `locations` list (currently a closure at graph materialization time) with a lambda reading `IOptionsMonitor<NjordOptions>.CurrentValue.Locations.Select(l => l.Name).ToList()` at each `SelectMany`/`Compute` invocation.
- [x] 5.2 Verify `EnrichmentActor` constructor receives `IOptionsMonitor<NjordOptions>` (not `IOptions`). Update `NjordActorSystemSetup.cs` registration if needed.
- [x] 5.3 Write `EnrichmentActorLiveLocationsSpec` in `src/Njord.Tests/Enrichment/`. Test: after options change, the locations passed to enrichment features reflect the updated config.

## 6. ConfigChangeCoordinator

- [x] 6.1 Create `ConfigChangeCoordinator` as `IHostedService` in `src/Njord/Configuration/`. Inject `IOptionsMonitor<NjordOptions>`, `ActorRegistry` (for SchedulerActor and DiscoveryActor refs). Register `OnChange` callback. Implement debounce (2s timer, reset on each change, process only final state).
- [x] 6.2 Implement diff logic: compare previous and current `NjordOptions` snapshots, compute added/removed `(Location, WeatherModel)` pairs using `LocationOptions.ResolveModels()`.
- [x] 6.3 Implement dispatch: validate new config via `NjordOptionsValidator.ValidateOptions()`, then send `AddTargets`/`RemoveTargets` to SchedulerActor and `RefreshDiscovery` to DiscoveryActor. Log validation failures without dispatching.
- [x] 6.4 Register `ConfigChangeCoordinator` in `src/Njord/Configuration/NjordServiceSetup.cs` as a hosted service.
- [x] 6.5 Write `ConfigChangeCoordinatorSpec` in `src/Njord.Tests/Configuration/`. Tests: model added dispatches AddTargets + RefreshDiscovery, model removed dispatches RemoveTargets + RefreshDiscovery, identical config produces no messages, invalid config logs error and skips dispatch, rapid changes debounced to single dispatch.

## 7. StatsCollector

- [x] 7.1 Create `StatsCollector` as `IHostedService` in `src/Njord/Telemetry/`. Use `MeterListener` to subscribe to the `"njord"` meter. Implement ring buffer (circular array per metric key, 30s interval, 24h retention).
- [x] 7.2 Implement `GetTimeSeries(metricName, window, dimensionFilter)` returning `IReadOnlyList<TimeSeriesPoint>` and `GetCurrentValues()` returning a snapshot record. Implement `GetBudgetStatus()` using fetch counter totals + config-based projection.
- [x] 7.3 Ensure thread-safety: `ConcurrentDictionary` for metric buckets, lock-free ring buffer reads.
- [x] 7.4 Register `StatsCollector` in `src/Njord/Configuration/NjordServiceSetup.cs`.
- [x] 7.5 Write `StatsCollectorSpec` in `src/Njord.Tests/Telemetry/`. Tests: captures counter increments, evicts old entries, GetTimeSeries returns windowed data, GetCurrentValues returns totals, concurrent read/write does not corrupt.

## 8. Admin API endpoints

- [x] 8.1 Create `src/Njord/Api/ConfigApi.cs` with minimal API endpoint group under `/api/config`: `GET /api/config` (full config with masked password), `PUT /api/config/{section}` (section update with validation), `GET /api/config/metadata` (hot-reload metadata), `GET /api/config/overrides` (active overrides), `DELETE /api/config/overrides/{key}` and `DELETE /api/config/overrides` (reset overrides).
- [x] 8.2 Create `src/Njord/Api/StatsApi.cs` with minimal API endpoint group under `/api/stats`: `GET /api/stats/timeseries?metric=&window=`, `GET /api/stats/current`, `GET /api/stats/budget`.
- [x] 8.3 Create `src/Njord/Api/HealthApi.cs` with `GET /api/health` returning structured component health (MQTT, pipeline, per-model scheduler state, enrichment feature status).
- [x] 8.4 Wire API endpoint groups in `src/Njord/Program.cs`: `app.MapGroup("/api/config").MapConfigApi()`, etc. Add `UseStaticFiles()` and SPA fallback middleware.
- [x] 8.5 Write `ConfigApiSpec` in `src/Njord.Tests/Api/`. Tests: GET returns full config, PUT validates and applies, PUT with invalid data returns 400, overrides endpoint lists active overrides, delete resets override. Use `WebApplicationFactory` or equivalent.
- [x] 8.6 Write `StatsApiSpec` in `src/Njord.Tests/Api/`. Tests: timeseries returns data points, current returns counters, budget returns projection.

## 9. Vue.js SPA

- [x] 9.1 Scaffold Vue 3 project at `ui/` with Vite, TypeScript, Vue Router, Chart.js. Add `package.json`, `tsconfig.json`, `vite.config.ts`. Configure proxy for `/api/*` to `http://localhost:8080` during dev.
- [x] 9.2 Create layout: sidebar navigation (Dashboard, Config, Health), main content area. Minimal CSS or Tailwind for styling.
- [x] 9.3 Build Config Editor page: section-based forms for General, Locations (CRUD), Models, Horizons, Parameters, Enrichment (per-feature toggles + settings), Budget Override. MQTT and Persistence sections display-only with restart-required note. Override indicators with per-field reset.
- [x] 9.4 Build Dashboard page: summary stat cards (fetches, failures, MQTT publishes, uptime, budget %), Chart.js line charts for fetch activity, fetch latency, MQTT publishes. Window selector (1h, 6h, 24h). Auto-refresh with 30s polling.
- [x] 9.5 Build Health page: component status cards (MQTT connection, pipeline health), per-model scheduler state table (phase, miss count, next poll), enrichment feature status list.
- [x] 9.6 Add API client module (`ui/src/api/`) with typed fetch wrappers for all `/api/*` endpoints.
- [x] 9.7 Verify `npm run build` produces a clean `dist/` with no external CDN references.

## 10. Docker integration

- [x] 10.1 Update `Dockerfile`: add Node build stage (`node:22-alpine`), `cd ui && npm ci && npm run build`. Copy `dist/` output to `wwwroot/` in the .NET publish stage.
- [x] 10.2 Verify the chiseled runtime image still builds and starts with the added static files.
- [x] 10.3 Test end-to-end: build Docker image, run container, verify SPA loads at `/`, API responds at `/api/config`, health endpoints unchanged.

## 11. Validation

- [x] 11.1 Run all existing tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` — no regressions.
- [x] 11.2 Run new unit tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Configuration.WritableMemoryConfigurationProviderSpec"` and similar for all new spec classes.
- [x] 11.3 Run `dotnet slopwatch` from repo root to check for code quality regressions.
- [x] 11.4 Run `npm run build` in `ui/` to verify SPA builds cleanly.
- [x] 11.5 Build Docker image and verify container starts with SPA accessible.
