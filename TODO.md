# TODO — week of 2026-10-26

Each item becomes its own OpenSpec change (`/opsx:explore` → `/opsx:propose`). Items 1–3 touch the same area
(OpenMeteo fetch path and requirement resolution), so explore them together first.

## 1. Rewrite the OpenMeteoGateway

- The current design does not fit; rewrite the gateway completely.
- Goal: higher throughput. Candidate direction is the FunkArr `MediathekViewWebManager` pattern: concurrency in a
  stream (`Source.Queue` + `SelectAsyncUnordered`), the actor only owns lifecycle, clients stay thick and stateless.
- Keep the request/response contract (`FetchDomain` → `FetchDomainCompleted` / `FetchDomainFailed` /
  `FetchDomainRejected`) and the budget gate semantics, unless the explore step decides otherwise.

## 2. Replace `WeatherResponseConverter` with clean API models

- Typed response DTOs that mirror the Open-Meteo JSON, then mapping extensions to the domain points
  (`<Domain><Target>MappingExtensions`), instead of a hand-written converter.

## 3. Clean up requirement and model resolution in `DomainSetupContainer`

Today the DI registrations carry logic:

- a raw `IReadOnlySet<DomainKind>` singleton from `FeatureResolution.RequiredDomains(...)`
- an `EffectiveWeatherModels` factory lambda that checks the `Weather:ModelComparison` rule, resolves the models
  and logs the ignored models from inside DI
- `new ResolvedRequirements(FeatureResolution.WithBaseParams(FeatureResolution.RequiredParams(...)))`

Idea: one resolved type (e.g. `FetchPlan`: domains, params, models) built once from the enabled rules and the
options, registered as a single singleton; the ignored-models warning is logged at startup, not in a DI factory.

## 4. Clean client design and a structured `Njord.OpenMeteo` project

- Find one clean pattern for the 6 clients (`WeatherClient`, `AirQualityClient`, `FloodClient`, `MarineClient`,
  `SeasonalClient`, `SatelliteRadiationClient`). Open question: a shared generic client plus a per-domain
  request/response definition, or 6 thick clients built on shared plumbing (HTTP, error mapping, trimming).
- Restructure the project. Today the root is flat and mixed: clients, `*Response` DTOs, `WeatherResponseConverter`,
  budget types (`GatewayBudgetGate`, `IGatewayBudgetGate`, `IBudgetProvider`, `OptionsBudgetProvider`,
  `BudgetRate`), `HttpMetricsHandler`, `ClientRequest`, `OpenMeteoJsonContext` and the gateway.
  Candidate layout: one folder per domain (`Weather/`, `AirQuality/`, `Flood/`, `Marine/`, `Seasonal/`,
  `Satellite/` with client, API models and mappings side by side), plus `Gateway/`, `Budget/`, `Http/`,
  `Configuration/`.
- Remove the many small static helpers: `Mapping/` holds 7 `*ParamsMappingExtensions`, 6
  `*ResponseMappingExtensions`, `DomainKindMappingExtensions`, `ForecastTrimmingExtensions` and `SeriesAccess`.
  Fold them into the domain folders or into the shared client plumbing; keep only the explicit enum mappings the
  conventions require.
- Do this together with items 1 and 2.

## 5. Remove the magic strings in `Njord.OpenMeteo`

- About 220 snake_case string literals across 26 files.
- Fine and stays: `[JsonPropertyName("...")]` in the `*Response` DTOs (normal JSON binding, documents the API
  shape) and the metric/tag names in `Telemetry` and `HttpMetricsHandler`.
- Target: the `*ParamsMappingExtensions` (`ToApiName(s)`, flag → `"temperature_2m"`, Weather alone 32), the query
  keys and fixed values in `ClientRequest` and the clients (`hourly`, `models`, `timeformat`, `unixtime`, ...),
  and the endpoint paths.
- Idea for `ToApiName`: put the API name as an attribute on each enum member instead of a switch, using a
  built-in .NET attribute (`[JsonStringEnumMemberName("temperature_2m")]` from System.Text.Json, or
  `[EnumMember(Value = "...")]`), read once into a cached lookup. To decide in explore:
  - The `XxxParams` enums live in `Njord.Forecast` (API-free vocabulary); the attributes would put Open-Meteo names
    there. Alternative: API-side enums or per-domain tables in `Njord.OpenMeteo`.
  - It replaces the AGENTS.md rule "enum to wire text = explicit switch, default throws"; update the convention
    and keep a spec that every member has a name.
- Query keys, fixed values and paths become named constants.

## 6. Design a clean options / config system

- Rethink the whole options and configuration model from scratch, so every consumer gets what it needs directly
  from one well-defined source.
- Goal: no derived helper types or factory registrations such as `EffectiveWeatherModels` or `ServiceInfo` that
  exist only to patch together values from several options classes and the enabled features.
- To decide in explore: which values are operator input (locations, feature toggles, domain options) and which are
  derived (domains, params, models, call weights), where the derived values are computed once and how they are
  exposed (one resolved type at startup, validated together). Overlaps with item 3; explore them together.

## 7. Look at `FeatureDescriptor`

- Needs a closer look: `Name` and `PollDepth` in `FeatureDescriptor`. `Name` is used for more than the config
  section, so this is not only a config question.

## 8. Rewrite all tests in the FunkArr style

- Rewrite every test project in the same style and approach as the FunkArr tests (`D:\GIT\FunkArr`).
- Order: after items 1–5, so the OpenMeteo tests are not rewritten twice.
- Decided:
  - Suffix `Tests` instead of `Spec` (classes and files).
  - Replace `NjordActorSpec` and the spec base classes completely.
  - Integration tests work with probes, as in FunkArr.
  - Layout 1:1 as in FunkArr, including the `XxxStateTests` pairs.
  - Naming as in FunkArr.
  - Test data, the shared project and data-only specs on the FunkArr level.
  - `Timeout` as in FunkArr: only async tests set it.
- Adapt the AGENTS.md rule "each enum conversion has a spec pinning every member" to the FunkArr level.
- Take over the FunkArr code and test conventions (FunkArr `AGENTS.md` and memories) where they fit njord;
  AGENTS.md and `ArchitectureSpec` follow.
