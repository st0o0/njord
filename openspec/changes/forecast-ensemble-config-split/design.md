## Context

`Njord:Weather` configures two distinct Open-Meteo APIs through a single section. `ForecastDays` applies to both with different caps (16 vs 35), `Models` is ensemble-only, `PastHours` is forecast-only. `FeatureRequirementsOptions` exists as an intermediate container to shuttle model IDs and derived fields from `FeatureResolution.Configure()` to consumers. `WeatherModelCatalog` hardcodes three ensemble-mean model IDs that could go stale. Both Forecast and Ensemble APIs support multi-model selection and the response parser (`SplitByModel`) already handles it, but `ForecastClient` hardcodes `best_match`.

## Goals / Non-Goals

**Goals:**
- Each Open-Meteo API gets its own config section with its own ForecastDays and settings
- Eliminate `FeatureRequirementsOptions`, `FeatureResolution`, and `WeatherModelCatalog`
- `DomainRequirements` becomes the single resolved dependency, registered as `IOptions<DomainRequirements>`
- Enable multi-model on the Forecast API (opt-in, default = best_match)
- BudgetGuard uses correct per-API ForecastDays

**Non-Goals:**
- God validator split, SolarYield bridge, options project placement (follow-up change)
- New forecast model comparison features
- Hot-reload / IOptionsMonitor

## Decisions

### D1: Two new options classes, `WeatherOptions` removed

`ForecastOptions` (`Njord:Forecast`): `ForecastDays` (4, 1..16), `PastHours` (0, 0..96), `Models` ([], `IHasArrayDefaults` with `DefaultModels => []`).

`EnsembleOptions` (`Njord:Ensemble`): `ForecastDays` (4, 1..35), `Models` ([], `IHasArrayDefaults` with `DefaultModels => [DefaultModel]` where `DefaultModel = "ecmwf_ifs025_ensemble_mean"`).

Both implement `IConfigSection`, registered via `AddNjordOptions<T, TValidator>()`. Each gets its own validator (`ForecastOptionsValidator`, `EnsembleOptionsValidator`) instead of being added to the god validator.

**Alternative**: Keep `WeatherOptions` as a facade that reads both sections. Rejected: adds complexity and the whole point is honest config.

### D2: `DomainRequirements` promoted to `sealed record` and registered as `IOptions<DomainRequirements>`

`DomainRequirements` changes from `readonly record struct` to `sealed record` (class). This makes it directly compatible with `IOptions<T>` (which requires a class with a parameterless constructor) without needing a wrapper.

The type keeps all its existing capabilities: the positional constructor with 14 defaulted parameters, the `|` operator, `with`-expressions, computed properties (`AllForecast`, `Requires()`), and `WithBaseParams()`.

Changes required:
- `readonly record struct` → `sealed record`
- `default(DomainRequirements)` → `new DomainRequirements()` (~5 call sites)
- Add parameterless constructor: `public DomainRequirements() : this(default, default, ..., default) { }`
- All `new DomainRequirements(Forecast: X, Ensemble: Y)` call sites (70+) are unchanged

Registration:

```csharp
services.AddOptions<DomainRequirements>()
    .Configure<IEnumerable<IFeatureRule>>((options, rules) =>
    {
        var resolved = rules
            .Where(r => r.Enabled)
            .Aggregate(new DomainRequirements(), (all, f) => all | f.Descriptor.Requirements)
            .WithBaseParams();
        options.Forecast = resolved.Forecast;
        options.AirQuality = resolved.AirQuality;
        // ... all 14 fields
    })
    .ValidateOnStart();
```

Note: because `Configure<>` mutates the existing instance (it cannot replace it), the fields must be individually assigned. To make this clean, add a `CopyFrom(DomainRequirements other)` method or make properties `{ get; set; }` instead of `{ get; init; }`.

Chosen approach: use `init` properties with the positional record syntax (records generate `init` setters), and assign fields in the Configure delegate. This works because the Options infrastructure creates the instance with the parameterless constructor and Configure mutates it before it is frozen.

Consumer access is clean: `IOptions<DomainRequirements>` → `.Value.AllForecast`. No wrapper, no double-unwrap.

**Alternative A**: Keep the struct and add a `DomainRequirementsOptions` wrapper class. Rejected: adds an unnecessary type and `.Value.Requirements.AllForecast` double-unwrap.
**Alternative B**: Register as a plain singleton. Rejected: loses `ValidateOnStart` and diverges from the Options pattern.

### D3: `WithBaseParams()` and `RequiredDomains()` on `DomainRequirements`

`WithBaseParams()` becomes an instance method on the struct (it only reads and transforms its own fields). `RequiredDomains()` becomes an extension method in a separate file (it uses `Enum.GetValues` which is a convenience, not core logic).

### D4: `WeatherModelCatalog` removed, no replacement

Model IDs come from the operator config. Validation: non-empty (Ensemble), no duplicates. Unknown IDs produce an API 400 at runtime, which is a normal fetch failure handled by the existing retry/backoff path. The gRPC catalog endpoint returns `EnsembleOptions.Models` directly.

**Alternative**: Fetch available models from the API at startup. Rejected: adds a startup dependency on the API and complexity for little value.

### D5: `EnsembleClient` owns multi-model decision

The client reads `EnsembleOptions.Models` and checks `AllEnsemble.HasFlag(EnsembleParams.Models)` (already implemented). When the flag is set, it uses all models; when not, only `Models[0]`. The `ComparisonFeatureName` string match is removed from `FeatureResolution` (which itself is removed). The `ModelComparison` feature's `ComparisonFeatureName` constant is removed.

### D6: `ForecastClient` multi-model opt-in

When `ForecastOptions.Models` is non-empty, `ForecastClient` sends `models=<list>` and uses `SplitByModel`/`ToModelForecasts` (already implemented but never exercised). When empty, it omits the parameter and uses `best_match` as today. The gateway model-dispatch path already handles per-model `DomainDataReceived`.

### D7: `BudgetGuard` takes per-API options

Signature changes to accept `ForecastOptions` and `EnsembleOptions` separately. Forecast weight uses `ForecastOptions.ForecastDays`, ensemble weight uses `EnsembleOptions.ForecastDays`. The `PollingOptionsValidator` injects `IOptions<ForecastOptions>` and `IOptions<EnsembleOptions>` instead of reading raw `IConfiguration`.

## Risks / Trade-offs

- **Breaking config change** → Acceptable at 0.x. docker-compose.example.yml, appsettings.Example.json, Config Builder, and docs all need updating. Users must migrate `Njord__Weather__*` env vars to `Njord__Forecast__*` and `Njord__Ensemble__*`.
- **No catalog validation for model IDs** → An invalid model ID will fail at runtime (API 400) instead of at startup. This is acceptable because the model list evolves outside our control and the error is clear.
- **DomainRequirements struct → class** → 90 files reference the type but ~70 are `new DomainRequirements(...)` which need no change. ~5 `default(DomainRequirements)` become `new DomainRequirements()`. Value semantics → reference semantics is safe because the type is immutable (record). The `|` operator, `with`-expressions, and positional constructor all work on `sealed record` classes.
- **PollingOptionsValidator cross-options access** → The raw `IConfiguration` workaround is replaced by injecting `IOptions<ForecastOptions>` and `IOptions<EnsembleOptions>`. If their own validators fail, the error is reported by those validators, not by PollingOptionsValidator. This is correct behavior.

## Migration Plan

1. Create `ForecastOptions` + `EnsembleOptions` + validators
2. Create `DomainRequirementsOptions` with PostConfigure registration
3. Migrate all consumers from `FeatureRequirementsOptions` → `DomainRequirementsOptions` + direct options
4. Update `BudgetGuard` + `PollingOptionsValidator` to use per-API options
5. Update `ForecastClient` for optional multi-model
6. Remove `WeatherOptions`, `WeatherModelCatalog`, `FeatureRequirementsOptions`, `FeatureResolution`
7. Update config files, docs, Config Builder
8. Update AGENTS.md / CLAUDE.md

## Open Questions

None - all decisions resolved during exploration.
