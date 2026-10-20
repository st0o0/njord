All commands run from `src/`. Each chunk ends green on build, `dotnet format Njord.slnx --verify-no-changes`, and the listed suites. New specs: `Spec` suffix, sealed class, `[Fact(Timeout = 5000)]`, BDD-style names. Commit per chunk (single-line Conventional Commit, no push).

## 1. Rename Njord.Model to Njord.Forecast (no functional change)

- [x] 1.1 `git mv src/Njord.Model src/Njord.Forecast` and `src/Njord.Model.Tests src/Njord.Forecast.Tests`; rename `.csproj` files, update `src/Njord.slnx`
- [x] 1.2 Move files into folders: `Weather/WeatherPoint.cs`, `AirQuality/AirQualityPoint.cs`, `Flood/FloodPoint.cs`, `Marine/MarinePoint.cs`, `Solar/SolarPoint.cs`, `Forecast/` (`DomainForecast.cs`, `IDomainForecast.cs`, `DomainKind.cs`, `Location.cs`, `IHasValidTime.cs`), `Horizon/` (`Horizon.cs`, `TimeResolution.cs`); mirror folders in `src/Njord.Forecast.Tests/`
- [x] 1.3 Replace namespace and usings `Njord.Model` with `Njord.Forecast` across `src/` (Messages, Persistence, Core, OpenMeteo, Pipeline, Weather, AirQuality, Flood, Marine, Mqtt, Grpc, Host and all test projects incl. `Njord.Tests.Shared`)
- [x] 1.4 Update all `ProjectReference` entries and `src/Njord.Architecture.Tests/ArchitectureSpec.cs` assembly names and rule names
- [x] 1.5 Validate: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, then every `Njord.*Tests` via `dotnet run --project <p>/<p>.csproj --no-build` (sequential)

## 2. Param enums

- [x] 2.1 Add `src/Njord.Forecast/Weather/WeatherParam.cs` (`[Flags] : long`), `AirQuality/AirQualityParam.cs`, `Flood/FloodParam.cs`, `Marine/MarineParam.cs` (`: int`), one flag per current API variable (see `WeatherVariables` in `src/Njord.Core/Configuration` and the constants in `src/Njord.OpenMeteo/*Client.cs`), including the ensemble spread variable
- [x] 2.2 Specs in `src/Njord.Forecast.Tests/<Domain>/<Domain>ParamSpec.cs`: distinct bits, union, WeatherParam beyond 32 bits
- [x] 2.3 Validate: `dotnet build Njord.slnx`; `dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj`

## 3. Nullable points and HasFields

- [x] 3.1 Make all fields except `ValidFrom` nullable in `WeatherPoint`, `AirQualityPoint`, `FloodPoint`, `MarinePoint` (`double?`, `int?` WeatherCode, `bool?` IsDay)
- [x] 3.2 Add `HasFields(XxxParam required)` to each Point (explicit switch per flag, default throws)
- [x] 3.3 Make response DTO arrays nullable (`double?[]?`) in `src/Njord.OpenMeteo/{Weather,AirQuality,Flood,Marine}Response.cs`; update `src/Njord.OpenMeteo/Mapping/` to pass null through (no 0 defaults)
- [x] 3.4 Update handlers in `src/Njord.Weather`, `Njord.AirQuality`, `Njord.Flood`, `Njord.Marine`, plus `Njord.Grpc/Mapping` and `Njord.Mqtt/Mapping` / `Payloads` for nullable fields
- [x] 3.5 Update builders/fakes in `src/Njord.Tests.Shared` and all affected tests; add `HasFields` specs in `Njord.Forecast.Tests` and null-passthrough specs in `Njord.OpenMeteo.Tests/Mapping`
- [x] 3.6 Validate: build, format, then ALL test suites sequentially (`dotnet run --project <p>/<p>.csproj --no-build`)

## 4. DomainRequirements and IDomainHandler

- [x] 4.1 Add `src/Njord.Core/Domain/DomainRequirements.cs` (record struct, four Param properties, `operator |`, `Requires(DomainKind)`)
- [x] 4.2 In `src/Njord.Core/Domain/IDomainHandler.cs` replace `Triggers` with `DomainRequirements Requirements { get; }`; update `EnrichmentResolution` to aggregate with `|` and derive required domains and per-domain params
- [x] 4.3 Declare `Requirements` in every handler in `src/Njord.Weather` (alert handler, Lifestyle/), `Njord.AirQuality`, `Njord.Flood`, `Njord.Marine`
- [x] 4.4 Specs: `src/Njord.Core.Tests/Domain/DomainRequirementsSpec.cs`, extend `EnrichmentResolution` specs; update handler specs in the four domain test projects; keep `EnrichmentConfigKeySpec` green
- [x] 4.5 Validate: build, format; `dotnet run --project` for Njord.Core.Tests, Njord.Weather.Tests, Njord.AirQuality.Tests, Njord.Flood.Tests, Njord.Marine.Tests, Njord.Architecture.Tests

## 5. Per-domain options and thin clients

- [x] 5.1 Add `AirQualityOptions`, `MarineOptions`, `FloodOptions` and `<T>Validator` in `src/Njord.Core/Configuration/`; register with `AddNjordOptions<T>` in the Host setup container (`src/Njord/Configuration/`)
- [x] 5.2 Add `src/Njord.OpenMeteo/Mapping/ParamMappings.cs` with `ToApiNames()` per Param enum (explicit switch, default throws)
- [x] 5.3 Simplify `WeatherClient`, `AirQualityClient`, `FloodClient`, `MarineClient` (and `I*Client` interfaces) to take `IReadOnlyList<string> variables` and `forecastDays`; remove constants
- [x] 5.4 Update `src/Njord.OpenMeteo/OpenMeteoGateway.cs` to use resolved params from `EnrichmentResolution` and per-domain ForecastDays; ensure the default config still weighs 4 weather calls (`BudgetCalculator`)
- [x] 5.5 Specs: validators (defaults, at limit, above limit, below min) in `Njord.Core.Tests`; `ParamMappingsSpec` pinning every member; client URL specs and gateway specs in `Njord.OpenMeteo.Tests`
- [x] 5.6 Validate: build, format; `dotnet run --project` for Njord.Core.Tests, Njord.OpenMeteo.Tests, Njord.IntegrationTests

## 6. Stream validation

- [x] 6.1 In `src/Njord.Pipeline/ProjectionEngine.cs` call `forecast.HasFields(handler.Requirements)` before `Handle`; skip and log a warning
- [x] 6.2 Specs in `src/Njord.Pipeline.Tests/ProjectionEngineSpec.cs`: complete data runs, missing field skips with no event, other handlers unaffected, absent domain skips
- [x] 6.3 Validate: build, format; `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`

## 7. Config and docs

- [x] 7.1 Add `Njord:AirQuality`, `Njord:Marine`, `Njord:Flood` sections to `docker-compose.example.yml` and `src/Njord/appsettings.Example.json`
- [x] 7.2 Update Config Builder (`docs/.vitepress/theme/builder/types.ts`, `budget.ts`, `serializer.ts`, `ConfigBuilder.vue`, `docs/data/enrichment.json`) so per-domain ForecastDays and triggers match
- [x] 7.3 Update `AGENTS.md` (structure, options table, rename, Triggers to Requirements) and e2e files (`e2e/`, `.claude/skills/e2e-test/SKILL.md`)
- [x] 7.4 Validate: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`; docs build if available

## 8. Final validation

- [x] 8.1 `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
- [x] 8.2 All suites sequentially: `for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done`
- [x] 8.3 Grep repo for leftover `Njord.Model` and `Triggers` references (excluding `openspec/changes/archive`)
- [x] 8.4 `openspec validate --all --no-interactive` and `bash scripts/check-openspec-root.sh`
