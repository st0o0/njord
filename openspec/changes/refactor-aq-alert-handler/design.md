## Context

`AirQualityAlertHandler` is instantiated three times via factory lambdas in DI, each with a different string name (`"Pollen"`, `"Pm25"`, `"Dust"`). Each instance receives all AQ alert rules and filters to its own. `WeatherAlertHandler` already implements `IMultiFeatureHandler` — a single instance that holds all weather rules, lazy-resolves the enabled subset, and exposes a composite `Feature` with united requirements.

## Goals / Non-Goals

**Goals:**
- Make `AirQualityAlertHandler` follow the same `IMultiFeatureHandler` pattern as `WeatherAlertHandler`.
- Eliminate the magic string constructor parameter and factory-lambda registrations.
- Enable per-feature toggling of AQ alerts via `FeatureOptions` (same as Weather).

**Non-Goals:**
- No changes to `WeatherAlertHandler`, flood handlers, or marine handlers.
- No extraction of a shared base class between Weather and AQ alert handlers (future work).
- No wire, gRPC, or configuration changes.

## Decisions

### 1. Same structure as WeatherAlertHandler

The handler follows the exact `WeatherAlertHandler` shape:
- Constructor: `IEnumerable<IAlertRule<DomainForecast<AirQualityPoint>>>`, `IOptions<AlertOptions>`, `IOptions<FeatureOptions>`
- `_rules`: all rules, ordered by name
- `_enabled`: `Lazy<Enabled>` resolved from `FeatureOptions`
- `Features`: all rules (for `FeatureResolution.FeaturesOf`)
- `Name`: `"AirQualityAlerts"`
- `Handle`: checks `options.Enabled` and `current.AirQuality`, then runs only enabled rules

Rejected: extracting a generic `MultiFeatureAlertHandler<T>` base. The two handlers are identical in structure but may diverge (Weather has `AlertWindow`, AQ has `AirQualityAlertWindow`). Premature abstraction.

### 2. Registration

```csharp
services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, PollenAlertRule>();
services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, Pm25AlertRule>();
services.AddSingleton<IAlertRule<DomainForecast<AirQualityPoint>>, DustAlertRule>();
services.AddSingleton<IDomainHandler, AirQualityAlertHandler>();
```

Same as Weather: rules as individual singletons, handler as `IDomainHandler` (not `IMultiFeatureHandler` — `FeatureResolution` detects it via runtime type check).

### 3. Test changes

The spec drops the `name` parameter from `Create()`. Instead:
- Test per-feature toggling (enable/disable individual AQ alerts via `FeatureOptions`)
- Test `Features` lists all 3 rules
- Test composite `Feature.Requirements` unions the enabled rules' requirements
- Keep the existing tests for alert evaluation and empty/disabled states

A `FeatureServices.AirQualityHandler(...)` helper mirrors `WeatherHandler(...)`.

## Risks / Trade-offs

- None. This is a mechanical refactor following an established pattern. The `IMultiFeatureHandler` contract and `FeatureResolution` integration are already proven by `WeatherAlertHandler`.
