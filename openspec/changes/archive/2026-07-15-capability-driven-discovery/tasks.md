## 1. sunrise/sunset Fix (Ingest Layer)

- [x] 1.1 Fix `OpenMeteoClient.MapDaily` to convert unix timestamps to ISO 8601 for `TimeString` parameters — in `src/Njord/Ingest/OpenMeteoClient.cs`, in `MapDaily`/`ParseMixedArray`, when a `TimeString` parameter's JSON value is a number, convert via `DateTimeOffset.FromUnixTimeSeconds(n).ToString("O")` instead of `raw.GetString()`
- [x] 1.2 Unit tests for sunrise/sunset conversion — in `src/Njord.Tests/Ingest/`, add specs covering: numeric unix timestamp → ISO 8601 string, string passthrough unchanged, null passthrough unchanged

## 2. Horizon Clamping & Null-Key Stripping (Egress Layer)

- [x] 2.1 Add `maxForecastHours` parameter to `HorizonProjection.BuildPerHorizon` in `src/Njord/Egress/HorizonProjection.cs` — filter configured horizons to only those ≤ `maxForecastHours`, filter daily day-offsets to `< ceil(maxForecastHours / 24)`, use `ModelCoverageRegistry.Get(model)?.MaxForecastHours` at the call site in `ModelStateActor`
- [x] 2.2 Strip null-valued keys from per-horizon JSON objects in `HorizonProjection.BuildPerHorizon` — when building the JSON object for a horizon, omit entries where the parameter value is null
- [x] 2.3 Unit tests for horizon clamping and null stripping — in `src/Njord.Tests/Egress/`, test: short-range model excludes far horizons, long-range model includes all, unknown model includes all, null keys omitted from JSON, all-null horizon excluded entirely

## 3. Capability Tracking (ModelStateActor)

- [x] 3.1 Define `ModelCapabilityLearned` message record in `src/Njord/Egress/` — sealed record with `Location`, `Model`, `SupportedParameters` (IReadOnlySet<ParameterDef>), `ApplicableHorizons` (IReadOnlyList<int>), `ApplicableDayOffsets` (IReadOnlyList<int>)
- [x] 3.2 Add capability tracking to `ModelStateActor` in `src/Njord/Egress/ModelStateActor.cs` — maintain `HashSet<ParameterDef>` per (location, model), union non-null parameters from each `FetchOutcome.Success`, send `ModelCapabilityLearned` to `DiscoveryActor` on first population and on expansion, resolve `DiscoveryActor` ref via DI/actor registry
- [x] 3.3 Update `ModelStateActor` call to `HorizonProjection.BuildPerHorizon` to pass `maxForecastHours` from `ModelCoverageRegistry`
- [x] 3.4 Unit tests for capability tracking — in `src/Njord.Tests/Egress/`, test: first fetch sends capability message, unchanged set does not re-emit, expanded set triggers update, failure does not affect tracked set, horizon capping from registry

## 4. Deferred Discovery (DiscoveryActor)

- [x] 4.1 Refactor `DiscoveryActor` in `src/Njord/Mqtt/DiscoveryActor.cs` to defer initial discovery — remove publish-on-connect, add state to collect `ModelCapabilityLearned` messages, track expected (location, model) count from config, schedule timeout (2× poll interval)
- [x] 4.2 Implement capability-filtered discovery publish — when all capabilities received or timeout fires, call `DiscoveryPayloadBuilder.Build` with each model's supported parameters and applicable horizons instead of the full config grid; publish enrichment devices unchanged
- [x] 4.3 Handle late capability arrivals — if a `ModelCapabilityLearned` arrives after initial publish (post-timeout or expanded parameters), publish/re-publish discovery for that specific device immediately
- [x] 4.4 Handle HA birth with learned state — on `homeassistant/status` = `online`, re-publish all discovery using current learned capabilities
- [x] 4.5 Unit tests for deferred discovery — in `src/Njord.Tests/Mqtt/`, test: no publish on connect, publish after all capabilities received, timeout triggers partial publish, late arrival triggers incremental publish, HA birth re-publishes with learned state, disabled mode ignores everything

## 5. Discovery Payload Filtering (DiscoveryPayloadBuilder)

- [x] 5.1 Add parameter and horizon filtering to `DiscoveryPayloadBuilder.Build` in `src/Njord/Mqtt/DiscoveryPayloadBuilder.cs` — accept `IReadOnlySet<ParameterDef> supportedParameters`, `IReadOnlyList<int> applicableHorizons`, `IReadOnlyList<int> applicableDayOffsets` parameters; only emit components for parameters in `supportedParameters` and horizons/day-offsets in the applicable lists
- [x] 5.2 Unit tests for filtered discovery payloads — in `src/Njord.Tests/Mqtt/`, test: unsupported parameter excluded, out-of-range horizon excluded, supported parameter + applicable horizon included, empty supported set produces device with no components

## 6. Validation

- [x] 6.1 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 6.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 6.3 Run E2E tests: `dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj` from `src/`
- [x] 6.4 Run slopwatch: `dotnet slopwatch` from repo root
