## 1. Parameter Registry (Domain Foundation)

- [x] 1.1 Create `src/Njord/Domain/ParameterDef.cs` — record with ApiName, Unit, DeviceClass, JsonKey, FriendlyName, Group (enum: Weather/Solar/Soil), Granularity (enum: Hourly/Daily), ValueType (enum: Numeric/TimeString). Equality by ApiName.
- [x] 1.2 Create `src/Njord/Domain/ParameterRegistry.cs` — static class with all ~50 hourly + ~20 daily Open-Meteo forecast variables as `ParameterDef` instances. Methods: `GetByGroup(group)`, `GetByApiName(name)`, `All`, `Resolve(groups, extra, exclude)` returning a `ResolvedParameterSet` (hourly + daily partitions).
- [x] 1.3 Create `src/Njord.Tests/Domain/ParameterRegistrySpec.cs` — tests: Weather group contains expected variables, Solar/Soil groups correct, Resolve with groups+extra+exclude, unknown name rejected, empty resolved set rejected, all entries have non-null Unit.

## 2. Domain Model Replacement

- [x] 2.1 Replace `src/Njord/Domain/ForecastPoint.cs` — new record: `ForecastPoint(DateTimeOffset ValidAt, IReadOnlyDictionary<ParameterDef, double?> Values)` with accessor `Get(ParameterDef)`. Remove old 9-field record.
- [x] 2.2 Create `src/Njord/Domain/DailyForecastPoint.cs` — record: `DailyForecastPoint(DateOnly Date, IReadOnlyDictionary<ParameterDef, object?> Values)` (object? to hold double? or string?).
- [x] 2.3 Create `src/Njord/Domain/DailyForecastSeries.cs` — ordered by Date, same pattern as `ForecastSeries` but for daily points.
- [x] 2.4 Update `src/Njord/Domain/ModelForecast.cs` — add `DailyForecastSeries Daily` property alongside existing `ForecastSeries` (renamed `Hourly`).
- [x] 2.5 Remove `src/Njord/Domain/WeatherParameter.cs` (enum + extensions).
- [x] 2.6 Update `src/Njord.Tests/Domain/ForecastSeriesSpec.cs` — rewrite against new dictionary-based ForecastPoint. Test: missing parameter survives, ordering normalized, all-null tail trimmed.

## 3. Configuration Extension

- [x] 3.1 Add `ParameterOptions` to `src/Njord/Configuration/NjordOptions.cs` — `Groups` (list of string, default `["Weather"]`), `Extra` (list of string, default empty), `Exclude` (list of string, default empty).
- [x] 3.2 Update `src/Njord/Configuration/NjordOptionsValidator.cs` — validate group names against enum, validate Extra/Exclude against registry, validate resolved set non-empty, update budget projection to use `ceil(hourlyCount / 10)` as weight.
- [x] 3.3 Register `ResolvedParameterSet` as singleton in DI (resolved once at startup from config + registry).
- [x] 3.4 Create `src/Njord.Tests/Configuration/ParameterOptionsValidationSpec.cs` — tests: unknown group rejected, unknown extra rejected, empty resolved rejected, budget projection with weight 3, over-budget rejected.

## 4. Ingest Layer (OpenMeteo Client)

- [x] 4.1 Rewrite `src/Njord/Ingest/OpenMeteoDtos.cs` — remove typed hourly/daily records. Keep `OpenMeteoForecastResponse` as envelope with `JsonElement Hourly` and `JsonElement? Daily` plus `OpenMeteoHourlyUnits`.
- [x] 4.2 Rewrite `src/Njord/Ingest/OpenMeteoClient.cs` — inject `ResolvedParameterSet`; build dynamic `hourly=...&daily=...` query string from active parameters; deserialize via `JsonElement.GetProperty(apiName)` per active param; map to new `ForecastPoint`/`DailyForecastPoint`; unit verification from registry metadata.
- [x] 4.3 Update `src/Njord/Ingest/OpenMeteoJsonContext.cs` — adjust source generator config for new DTO shape (envelope only, not per-variable).
- [x] 4.4 Rewrite `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` — tests: dynamic variable list in request URL, successful hourly+daily mapping, missing array treated as nulls, unit mismatch on dynamic parameters, model-unavailable still typed failure.

## 5. Egress Layer (MQTT Discovery + State)

- [x] 5.1 Remove `src/Njord/Egress/ParameterKeys.cs` — functionality now lives in registry.
- [x] 5.2 Update `src/Njord/Egress/TopicScheme.cs` — `UniqueId` takes `ParameterDef` + horizon (hourly: `h{n}`, daily: `d{n}`). Use `param.JsonKey` for the entity-id segment.
- [x] 5.3 Rewrite `src/Njord/Egress/DiscoveryPayloadBuilder.cs` — iterate `ResolvedParameterSet.Hourly` × horizons + `ResolvedParameterSet.Daily` × day-offsets. Derive unit/device_class from `ParameterDef`.
- [x] 5.4 Rewrite `src/Njord/Egress/StatePayloadBuilder.cs` — build JSON with `h3..h72` keys (hourly values) + `d0..d3` keys (daily values), all parameter values from dictionary. Handle TimeString values as JSON strings.
- [x] 5.5 Rewrite `src/Njord.Tests/Egress/DiscoveryPayloadSpec.cs` — tests: component count matches grid (hourly params × horizons + daily params × days), registry metadata flows to payload, no device_class when null.
- [x] 5.6 Rewrite `src/Njord.Tests/Egress/StatePayloadSpec.cs` — tests: state JSON shape with hourly + daily keys, missing values as null, sunrise as string.

## 6. Pipeline Integration

- [x] 6.1 Update `src/Njord/Program.cs` — register `ResolvedParameterSet` singleton, pass to client and egress builders.
- [x] 6.2 Verify pipeline actors (`PipelineGuardianActor`, `MqttConnectionActor`) pass `ResolvedParameterSet` to discovery/state builders without changes to actor protocols.
- [x] 6.3 Update `src/Njord/Configuration/NjordOptions.cs` — ensure `ForecastDays` (default 4) is accessible for daily day-offset derivation.

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass.
- [x] 7.2 Run `dotnet build Njord.slnx` from `src/` — no warnings, no errors.
- [x] 7.3 Run `dotnet slopwatch` from repo root — no regressions flagged.
