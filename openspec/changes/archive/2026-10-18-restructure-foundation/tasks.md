## 1. Remove dead ForecastSeries-based methods

- [x] 1.1 Remove `ForecastSeries.Mean()`, `.Values()`, `.Window()` from `src/Njord.Weather/ForecastSeries.cs` and their tests from `src/Njord.Weather.Tests/ForecastSeriesQuerySpec.cs`
- [x] 1.2 Remove `DerivedFormulas.DiurnalAmplitude(ForecastSeries, ...)` and `DerivedFormulas.SunshinePercent(ForecastSeries, ...)` from `src/Njord.Derived/DerivedFormulas.cs` and their tests from `src/Njord.Derived.Tests/DerivedFormulasSpec.cs`
- [x] 1.3 Remove `TrendFormulas.PrecipitationTiming(ForecastSeries, ...)` and `TrendFormulas.ExtremaTiming(ForecastSeries, ...)` from `src/Njord.Trends/TrendFormulas.cs` and their tests from `src/Njord.Trends.Tests/TrendFormulasSpec.cs`
- [x] 1.4 Update test count in `CLAUDE.md` (Derived, Trends, Weather totals will decrease) and verify `DocumentationArchitectureSpec` passes

## 2. Introduce ForecastValue

- [x] 2.1 Create `ForecastValue` readonly record struct in `src/Njord.Weather/ForecastValue.cs` with `double? Number`, `string? Text`, `bool HasValue`, and implicit conversions from `double?` and `string?`
- [x] 2.2 Add `ForecastValueSpec` tests in `src/Njord.Weather.Tests/ForecastValueSpec.cs` covering numeric, text, absent, equality, and implicit conversion scenarios
- [x] 2.3 Update `ForecastPoint` in `src/Njord.Weather/ForecastPoint.cs`: change `Values` dictionary from `IReadOnlyDictionary<ParameterDef, double?>` to `IReadOnlyDictionary<ParameterDef, ForecastValue>`, update `Get()` to return `ForecastValue`
- [x] 2.4 Update `DailyForecastPoint` in `src/Njord.Weather/DailyForecastPoint.cs`: replace `NumericValues` and `MetaValues` with a single `Values` dictionary of `IReadOnlyDictionary<ParameterDef, ForecastValue>`, replace `GetNumeric()`/`GetMeta()` with `Get()`, update `HasAnyValue`
- [x] 2.5 Update `HorizonValue` in `src/Njord.Weather/HorizonValue.cs` to `(ParameterDef Parameter, ForecastValue Value)` and update `HorizonProjection` in `src/Njord.Weather/HorizonProjection.cs`
- [x] 2.6 Update `OpenMeteoSeriesMappings` in `src/Njord.OpenMeteo/Mapping/OpenMeteoSeriesMappings.cs` — `ToForecastSeries` and `ToDailySeries` to construct `ForecastValue` entries
- [x] 2.7 Update all consumers of `ForecastPoint.Get()` that expect `double?`: `src/Njord.Consensus/ConsensusBuilder.cs` (use `.Number`), `src/Njord.Derived/DerivedFormulas.cs`, `src/Njord.Trends/TrendFormulas.cs`, `src/Njord.Grpc/Mapping/ForecastProtoMappings.cs`
- [x] 2.8 Update all consumers of `DailyForecastPoint.GetNumeric()`/`GetMeta()`: `src/Njord.Consensus/ConsensusBuilder.cs`, `src/Njord.Weather/HorizonProjection.cs`, `src/Njord.Weather/ForecastDataHash.cs`, `src/Njord.Grpc/Mapping/ForecastProtoMappings.cs`
- [x] 2.9 Update consumers of `HorizonValue.Number`/`.Text`: `src/Njord.Mqtt/Mapping/ForecastPayloadMappings.cs`
- [x] 2.10 Update all test files constructing `ForecastPoint` or `DailyForecastPoint` (dictionary value type changes from `double?` to `ForecastValue`; implicit conversion handles most cases but `DailyForecastPoint` tests need restructuring from two dicts to one)
- [x] 2.11 Update test count in `CLAUDE.md` (Weather tests increase due to ForecastValueSpec) and verify `DocumentationArchitectureSpec` passes
- [x] 2.12 Build and run all test suites to verify no regressions

## 3. Create Njord.Model project and move vocabulary types

- [x] 3.1 Create `src/Njord.Model/Njord.Model.csproj` with zero project references, same TFM and settings as Njord.Weather
- [x] 3.2 Move vocabulary types from `src/Njord.Weather/` to `src/Njord.Model/` with namespace rename `Njord.Weather` → `Njord.Model`: `ParameterDef.cs`, `ParameterRegistry.cs`, `WeatherModel.cs`, `LocationId.cs`, `Horizon.cs`, `CycleId.cs`, `WeatherCondition.cs`, `ResolvedParameterSet.cs`, `ForecastValue.cs`
- [x] 3.3 Rename `src/Njord.Weather/` to `src/Njord.Forecast/`, rename `.csproj`, update namespace to `Njord.Forecast` on remaining files: `Forecast.cs`, `ForecastSeries.cs`, `ForecastPoint.cs`, `DailyForecastSeries.cs`, `DailyForecastPoint.cs`, `ModelForecasts.cs`, `HorizonProjection.cs`, `TimeAnchor.cs`, `ForecastDataHash.cs`, `PollOutcome.cs`, `HorizonValue.cs`
- [x] 3.4 Add `<ProjectReference>` from `Njord.Forecast` to `Njord.Model`, add `using Njord.Model` where Forecast types reference Model types
- [x] 3.5 Move `LocationOptions` from `src/Njord.Weather/` (now gone) to `src/Njord.Core/Configuration/LocationOptions.cs`, update namespace to `Njord.Core.Configuration`

## 4. Update project references across the solution

- [x] 4.1 Update `Njord.Consensus.csproj`: replace Weather reference with Model + Forecast
- [x] 4.2 Update `Njord.Messages.csproj`: replace Weather reference with Model + Forecast (keeps Sensors, Consensus)
- [x] 4.3 Update `Njord.Core.csproj`: replace Weather reference with Model + Forecast (keeps Messages, Persistence, Consensus)
- [x] 4.4 Remove all `<ProjectReference>` to `Njord.Weather` from every other `.csproj` (feature libraries reference only Core; Core transitively provides Model and Forecast)
- [x] 4.5 Update `Njord.csproj` (host): replace Weather reference with Model (it already gets Forecast transitively through Core)

## 5. Namespace rename across all files

- [x] 5.1 Replace `using Njord.Weather` with `using Njord.Model` in files that only use vocabulary types (ParameterDef, ParameterRegistry, WeatherModel, LocationId, Horizon, CycleId, WeatherCondition, ResolvedParameterSet, ForecastValue)
- [x] 5.2 Replace `using Njord.Weather` with `using Njord.Forecast` in files that use time-series types (Forecast, ForecastSeries, ForecastPoint, etc.)
- [x] 5.3 Add both `using Njord.Model` and `using Njord.Forecast` in files that use types from both projects
- [x] 5.4 Update `PipelineEvent.cs` alias: `using WeatherForecast = Njord.Weather.Forecast` → `using WeatherForecast = Njord.Forecast.Forecast`
- [x] 5.5 Update `LocationOptions` references from `Njord.Weather` to `Njord.Core.Configuration`
- [x] 5.6 Build the solution and fix any remaining compilation errors

## 6. Update test projects

- [x] 6.1 Create `src/Njord.Model.Tests/Njord.Model.Tests.csproj` — move vocabulary-type tests from `src/Njord.Weather.Tests/` (ParameterRegistrySpec, WeatherModelSpec, HorizonSpec, CycleIdSpec, LocationIdSpec, WeatherConditionSpec, ResolvedParameterSetSpec, ForecastValueSpec)
- [x] 6.2 Rename `src/Njord.Weather.Tests/` to `src/Njord.Forecast.Tests/` — rename `.csproj`, update namespaces on remaining test files (ForecastSpec, ForecastPointSpec, DailyForecastPointSpec, ForecastSeriesSpec, HorizonProjectionSpec, ForecastDataHashSpec, ModelForecastsSpec, TimeAnchorSpec, PollOutcomeSpec)
- [x] 6.3 Update `<ProjectReference>` in both new test projects to point to `Njord.Model` / `Njord.Forecast` + `Njord.Tests.Shared`
- [x] 6.4 Update namespace imports (`using Njord.Weather` → `using Njord.Model` / `using Njord.Forecast`) across all other test projects that construct forecast or model types
- [x] 6.5 Update `Njord.Tests.Shared` references and namespace imports (AnalysisFixture and other shared helpers use Weather types)
- [x] 6.6 Build and run all test suites to verify compilation and passing tests

## 7. Reorganize solution folders

- [x] 7.1 Rewrite `src/Njord.slnx` with nine solution folders: `/Model/` (Model, Sensors), `/Forecast/` (Forecast), `/Contracts/` (Consensus, Messages), `/Infrastructure/` (Persistence, Core), `/Ingest/` (OpenMeteo, SensorHub, Budget), `/Analysis/` (Alerts, Derived, Trends, Indices), `/Processing/` (Pipeline), `/Egress/` (Mqtt, Grpc), `/Tests/` (all test projects). Host `Njord/` at solution root.
- [x] 7.2 Verify the solution loads and builds correctly after folder restructure

## 8. Update ArchUnit specs and documentation

- [x] 8.1 Update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs`: add rules for Njord.Model (zero references), Njord.Forecast (only Model), update Consensus rule (Model + Forecast), update Messages rule (Model + Sensors + Consensus + Forecast), update Core rule (Model + Forecast + Messages + Persistence + Consensus), remove Weather rules
- [x] 8.2 Update `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`: new zone assignments matching the nine-folder structure, verify analysis features cannot reference Forecast
- [x] 8.3 Update any other ArchUnit specs that reference `Njord.Weather` by name (NamingArchitectureSpec, MqttBoundarySpec, PersistenceBoundarySpec, etc.)
- [x] 8.4 Update `CLAUDE.md`: solution structure section (replace Weather with Model + Forecast), reference direction, zone descriptions, `.slnx` folder layout, test counts
- [x] 8.5 Update `AGENTS.md` if it references Njord.Weather by name
- [x] 8.6 Update `DocumentationArchitectureSpec` expectations: new project names, updated test counts, updated file/spec names
- [x] 8.7 Run all Architecture.Tests to verify everything passes

## 9. Validation

- [x] 9.1 `dotnet build Njord.slnx` — clean build with zero warnings related to the restructure
- [x] 9.2 Run all test suites sequentially and verify the full count passes:
  ```bash
  for p in Njord.*Tests; do
    [ "$p" = Njord.Tests.Shared ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
- [x] 9.3 Verify `grep -r "Njord.Weather" src/ --include="*.cs" | grep -v obj/` returns zero results
- [x] 9.4 Verify `grep -r "Njord.Weather" src/ --include="*.csproj" | grep -v obj/` returns zero results
- [x] 9.5 Run `openspec validate --all --no-interactive` from repo root
