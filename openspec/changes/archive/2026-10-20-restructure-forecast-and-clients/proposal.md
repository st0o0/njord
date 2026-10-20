## Why

After removing the Geocoding and Climate Change APIs, the OpenMeteo client layer and the shared model project carry structural debt:

1. **`Njord.Model` is a generic name** that says nothing about its content. It holds forecast data types (points, domains, locations, horizons) and should be called `Njord.Forecast`.
2. **API variables are hardcoded constants** in each client (`WeatherClient`, `AirQualityClient`, `FloodClient`, `MarineClient`). A new enrichment that needs a new variable means editing the client, which is the wrong layer.
3. **Handlers do not declare which variables they need.** `IDomainHandler.Triggers` names domains but not parameters, so the system requests a superset and hopes the right fields arrive.
4. **ForecastDays coupling bug.** AirQuality uses `WeatherOptions.ForecastDays`, but the API caps at 7 days. Larger values silently return null.
5. **Point types mix required and optional fields.** `WeatherPoint` has 12 non-nullable `double` fields (0 when missing) next to 15 nullable ones. Which fields are present depends on what was requested, so all must be nullable.
6. **No validation that requested data arrived.** If the API drops a variable, handlers silently see `default(0)`.

## What Changes

1. **Rename `Njord.Model` to `Njord.Forecast`**: project, namespace, test project, domain folders (`Weather/`, `AirQuality/`, `Flood/`, `Marine/`, `Solar/`, `Forecast/`, `Horizon/`), all references, `ArchitectureSpec`, slnx.
2. **`[Flags]` param enums per domain** in `Njord.Forecast`: `WeatherParam : long`, `AirQualityParam : int`, `FloodParam : int`, `MarineParam : int`. Each flag maps 1:1 to an API variable and lives beside its Point type.
3. **Nullable Point fields**: every field except `ValidFrom` becomes nullable on `WeatherPoint`, `AirQualityPoint`, `FloodPoint`, `MarinePoint`. Each Point gets `HasFields(XxxParam required)`. All mappings, handlers, gRPC/MQTT mappings and tests are updated.
4. **`DomainRequirements` in `Njord.Core`**: `readonly record struct` with one Param property per domain, `|` operator, `Requires(DomainKind)`. `IDomainHandler.Requirements` replaces `Triggers`. `EnrichmentResolution` aggregates via `|` and derives the required domains.
5. **Per-domain options**: `AirQualityOptions` (`Njord:AirQuality`, ForecastDays 5, max 7), `MarineOptions` (`Njord:Marine`, 8, max 8), `FloodOptions` (`Njord:Flood`, 92, max 210), each with an `IValidateOptions<T>` validator. Fixes the coupling bug.
6. **Thin clients**: clients receive `IReadOnlyList<string>` variables and `forecastDays` from the gateway; no const variable strings. `ToApiNames()` extensions in `Njord.OpenMeteo` map flags to API names.
7. **Stream validation** in `ProjectionEngine`: before each handler runs, check `forecast.HasFields(handler.Requirements)`; skip and log a warning when data is missing.
8. **Fully nullable response DTOs**: all arrays become `double?[]?`; mapping passes null through, no silent 0 defaults.

## Non-goals

- Changing the polling architecture or `PollWorker`.
- Adding new API parameters (`past_days`, `start_date`, ...); this restructuring only enables that later.
- Changing the pipeline/stream topology.
- Modifying the gRPC proto definitions.
- Adding new enrichments.

## API-budget impact

No change to polling frequency or call count. The same variables are requested, now driven by handler declarations instead of client constants. Weather call weight stays `ceil(variables / 10)`; the resolved variable set must be checked so that default configurations still weigh 4 calls. Per-domain ForecastDays may only reduce AirQuality payloads for users who had ForecastDays > 7 (previously silently null). Requests per month stay within the 300k free-tier budget; the `BudgetCalculator` formula is unchanged.

## Capabilities

### New Capabilities
- `domain-requirements`: handlers declare required API parameters; resolution aggregates them.
- `param-enums`: per-domain `[Flags]` parameter enums and `HasFields` on Points.
- `thin-clients`: clients take variables and forecast days from the caller.
- `stream-validation`: pipeline guards handler dispatch on data presence.
- `per-domain-options`: AirQuality/Marine/Flood options with API-limit validation.

### Modified Capabilities
- `domain-point-types`: all Point fields except `ValidFrom` are nullable.
- `enrichment-framework`: `Triggers` replaced by `Requirements`.
- Renamed: `Njord.Model` to `Njord.Forecast` (no behavior change).

## Impact

- **Njord.Forecast** (renamed from Model): 12 files restructured into folders, 4 new Param enum files, Points nullable plus `HasFields`.
- **Njord.Forecast.Tests** (renamed): nullable and Param/HasFields specs.
- **Njord.Core / Core.Tests**: `DomainRequirements`, `IDomainHandler`, `EnrichmentResolution`, new options and validators.
- **Njord.OpenMeteo / OpenMeteo.Tests**: four clients simplified, gateway modified, ParamMappings, nullable DTOs.
- **Njord.Weather, AirQuality, Flood, Marine** (+ tests): handlers declare `Requirements`, handle nullable points.
- **Njord.Messages, Njord.Persistence**: usings updated.
- **Njord.Grpc, Njord.Mqtt**: mappings handle nullable, usings updated.
- **Njord.Pipeline**: `ProjectionEngine` validation guard.
- **ArchitectureSpec**: assembly names and rules renamed.
- **Config**: new options sections, `docker-compose.example.yml`, `appsettings.Example.json`, Config Builder, AGENTS.md, e2e files.
