## Context

Today the pipeline evaluates features through `IDomainHandler.Handle(LocationForecast, LocationForecast?, TimeProvider)`. 22 handler classes and 19 alert rule classes are registered as DI singletons. Alerts use a three-layer stack (`IAlertRule<T>` → container handler → `IMultiFeatureHandler`); all other features use a single-layer `IDomainHandler`. `ProjectionEngine` keeps `Current` and `Previous` per location in a `LocationSlot` and iterates handlers whose requirements match the received domain.

Feature identity is `IFeature.Name` (string), used for config lookup (`FeatureOptions` dictionary), wire keys (`WireKey()` = `Name.ToLowerInvariant()`), and `features.json`. `AlertOptions` bundles all 19 alert threshold sets in one class. `FeatureResult` and its 5 subtypes carry `string WireKey` and generic `IReadOnlyDictionary<string, string>` data. The proto contract uses `string type_name` on `EnrichmentEvent` and `string type` + `map<string, string> data` on `LifestyleUpdate`.

## Goals / Non-Goals

**Goals:**
- One abstraction (`FeatureRule<TInput>`) for all feature types.
- Features receive only the domains they declared — compile-time enforcement.
- Features can access poll history via `PollDepth` declaration.
- Each feature has its own typed config via `IOptions<T>`.
- Feature identity is the C# type — no string name, no enum.
- gRPC sends typed per-feature proto messages — no string identifiers.
- Adding a new feature requires: one `FeatureRule<TInput>` class, one config type, one proto message, two DI lines.

**Non-Goals:**
- Parallel feature evaluation (CPU-bound microseconds, not worth the complexity).
- Ring buffer persistence across restarts (acceptable for 0.x).
- ha-njord changes (separate follow-up).
- New features or domains.

## Decisions

### 1. FeatureRule<TInput> and IFeatureRule

```csharp
public interface IFeatureRule
{
    FeatureDescriptor Descriptor { get; }
    bool Enabled { get; }
}

public abstract class FeatureRule<TInput> : IFeatureRule where TInput : struct
{
    public abstract FeatureDescriptor Descriptor { get; }
    public abstract bool Enabled { get; }
    protected abstract TInput? Project(LocationForecast forecast);
    protected abstract FeatureResult? Evaluate(FeatureContext<TInput> ctx);

    // Called by FeatureEngine — internal, not part of the public feature API
    internal IReadOnlyList<PipelineEvent> Execute(
        RingBuffer<LocationForecast> ring, DateTimeOffset now) { ... }
}
```

`IFeatureRule` is the non-generic interface for DI collection and resolution. `FeatureRule<TInput>` is the abstract base every feature extends. The `Execute` method is `internal` — the engine calls it; features never see it. Features implement only `Project` and `Evaluate`.

`Enabled` is exposed directly on the feature (read from `IOptions<T>.Value.Enabled` in the constructor) so the engine can filter without knowing the config type.

Rejected: keeping `IDomainHandler` alongside. The whole point is one abstraction. Rejected: making features delegates/functions — classes give a stable type identity and are DI-friendly.

### 2. Typed input records

```csharp
public readonly record struct WeatherInput(DomainForecast<WeatherPoint> Weather);
public readonly record struct WeatherAqInput(DomainForecast<WeatherPoint> Weather, DomainForecast<AirQualityPoint> AirQuality);
public readonly record struct WeatherSolarInput(DomainForecast<WeatherPoint> Weather, DomainForecast<SatelliteRadiationPoint> Satellite);
public readonly record struct FloodWeatherInput(DomainForecast<FloodPoint> Flood, DomainForecast<WeatherPoint> Weather);
public readonly record struct FloodMarineWeatherInput(DomainForecast<FloodPoint> Flood, DomainForecast<MarinePoint> Marine, DomainForecast<WeatherPoint> Weather);
public readonly record struct MarineWeatherInput(DomainForecast<MarinePoint> Marine, DomainForecast<WeatherPoint> Weather);
public readonly record struct SeasonalWeatherInput(DomainForecast<SeasonalPoint> Seasonal, DomainForecast<WeatherPoint> Weather);
public readonly record struct AqInput(DomainForecast<AirQualityPoint> AirQuality);
```

These live in `Njord.Core/Domain/FeatureInputs.cs`. A feature needing a combination not covered defines its own input record (rare). The `Project` method on each feature constructs its input from `LocationForecast`:

```csharp
// In FrostAlert:
protected override WeatherInput? Project(LocationForecast f) =>
    f.Weather is { } w ? new(w) : null;

// In CoastalFloodAlert (cross-domain):
protected override FloodMarineWeatherInput? Project(LocationForecast f) =>
    (f.Flood, f.Marine, f.Weather) is ({ } fl, { } m, { } w) ? new(fl, m, w) : null;
```

Rejected: generic projection via reflection (runtime cost, no compile-time safety). Rejected: single `FeatureInput` god struct with all nullable domains (defeats the purpose).

### 3. FeatureContext and PollDepth

```csharp
public readonly record struct FeatureContext<TInput>(
    TInput Current,
    IReadOnlyList<TInput> History,  // [0] = current, [1] = previous, ...
    DateTimeOffset Now) where TInput : struct;
```

`PollDepth` is declared in `FeatureDescriptor`. The engine projects each ring buffer entry through the feature's `Project`, collecting up to `PollDepth + 1` entries. Entries where `Project` returns null are excluded (the domain wasn't available for that poll).

Most features (alerts, scores, conditions) declare `PollDepth = 0` and get `History.Count == 1`. Trend features like `ForecastDrift` declare `PollDepth = 8` and get up to 9 entries. Features with `PollDepth > 0` where fewer entries than `PollDepth + 1` are available can decide to skip (return null) or work with partial history.

### 4. FeatureEngine and RingBuffer

```csharp
internal sealed class FeatureEngine
{
    private readonly IReadOnlyList<IFeatureRule> _enabled;
    private readonly int _ringDepth;
    private readonly Dictionary<string, RingBuffer<LocationForecast>> _rings = new(StringComparer.OrdinalIgnoreCase);

    public FeatureEngine(IEnumerable<IFeatureRule> allFeatures)
    {
        _enabled = [.. allFeatures.Where(f => f.Enabled)];
        _ringDepth = _enabled.Max(f => f.Descriptor.PollDepth) + 1;
    }

    public DomainRequirements Requirements =>
        _enabled.Aggregate(default(DomainRequirements), (all, f) => all | f.Descriptor.Requirements);
}
```

`RingBuffer<T>` is a simple fixed-size circular buffer in `Njord.Core`. The engine replaces `ProjectionEngine`'s handler loop: it updates the `LocationForecast`, pushes it into the ring, then calls `Execute` on each enabled feature whose requirements are met by the current forecast. The static `Evaluate` helper in `ProjectionEngine` takes `FeatureEngine` instead of `IReadOnlyList<IDomainHandler>`.

The ring buffer does NOT survive restarts. After a restart, features with `PollDepth > 0` will only produce results after `PollDepth` polls have completed (typically `PollDepth × 60 min` with default polling).

### 5. Per-feature config types

Each feature gets its own sealed config class in its project:

```csharp
// In Njord.Alerts/Configuration/FrostConfig.cs
public sealed class FrostConfig
{
    public bool Enabled { get; set; } = true;
    public double Moderate { get; set; } = 2.0;
    public double Warning { get; set; } = 0.0;
    public double Severe { get; set; } = -5.0;
}
```

Registration uses the existing `AddNjordOptions<T>` pattern:

```csharp
services.AddNjordOptions<FrostConfig>(config, "Njord:Features:Weather:Frost");
services.AddSingleton<IFeatureRule, FrostAlert>();
```

The config section path encodes `Category:FeatureName` for human readability in `appsettings.json`. This path is a registration-time constant, not derived from the feature at runtime.

`AlertOptions` is deleted; its thresholds are distributed to per-feature configs. `FeatureOptions` is deleted; its toggle logic is replaced by per-feature `Enabled` flags. `AlertOptionsValidator` is replaced by per-feature validators.

### 6. Typed result hierarchy

```csharp
// In Njord.Messages/Pipeline
public abstract record FeatureResult : PipelineEvent;

// Base per OutputKind
public abstract record AlertFeatureResult(Location Location, AlertSeverity Severity,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt) : FeatureResult;
public abstract record ScoreFeatureResult(Location Location, int Score,
    DateTimeOffset BestStart, int BestHours) : FeatureResult;
public abstract record ConditionFeatureResult(Location Location, string State) : FeatureResult;
public abstract record TrendFeatureResult(Location Location, string Direction, double Magnitude) : FeatureResult;
public abstract record ForecastFeatureResult(Location Location) : FeatureResult;

// Feature-specific sealed records
public sealed record FrostAlertResult(Location Location, AlertSeverity Severity,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt,
    double MinTemperature, double DewPoint)
    : AlertFeatureResult(Location, Severity, StartsAt, EndsAt);
```

Each feature returns its own sealed result type. `FeatureEngine.Execute` wraps the non-null result as a `PipelineEvent` — since `FeatureResult` extends `PipelineEvent`, it flows through the existing `BroadcastHub` unchanged.

The old generic `FeatureResult(Location, WireKey, OutputKind)` base with `string WireKey` is deleted.

### 7. Proto contract: typed oneof

```protobuf
// Base messages
message AlertData {
  njord.v2.Severity severity = 1;
  google.protobuf.Timestamp starts_at = 2;
  google.protobuf.Timestamp ends_at = 3;
}
message ScoreData {
  int32 score = 1;
  google.protobuf.Timestamp best_start = 2;
  int32 best_hours = 3;
}
message ConditionData {
  string state = 1;
}

// Per-feature messages
message FrostAlert {
  AlertData alert = 1;
  double min_temperature = 2;
  double dew_point = 3;
}
message BbqScore {
  ScoreData score = 1;
}

// Event
message EnrichmentEvent {
  string location = 1;
  google.protobuf.Timestamp updated_at = 2;
  oneof feature {
    FrostAlert frost = 10;
    HeatAlert heat = 11;
    StormAlert storm = 12;
    // ... one field per feature, field numbers stable
    BbqScore bbq = 50;
    LaundryScore laundry = 51;
    // ...
  }
}
```

The gRPC mapping in `Njord.Grpc` pattern-matches on the C# result type:

```csharp
static EnrichmentEvent ToProto(FeatureResult result) => result switch
{
    FrostAlertResult f => new() { Location = f.Location.Name, Frost = f.ToProto(), ... },
    BbqScoreResult b => new() { Location = b.Location.Name, Bbq = b.ToProto(), ... },
    _ => throw new ArgumentOutOfRangeException(...)
};
```

`GetEnrichmentsResponse` changes to `repeated EnrichmentEvent events` or a flat list of typed feature results. The exact shape will be settled during implementation to minimize churn in the `GrpcConsumer`.

### 8. FeatureResolution rewrite

`FeatureResolution` simplifies to:

```csharp
public static class FeatureResolution
{
    public static IReadOnlyList<IFeatureRule> EnabledFeatures(IEnumerable<IFeatureRule> features) =>
        [.. features.Where(f => f.Enabled)];

    public static DomainRequirements RequiredParams(IEnumerable<IFeatureRule> features) =>
        EnabledFeatures(features).Aggregate(default(DomainRequirements), (all, f) => all | f.Descriptor.Requirements);

    public static IReadOnlySet<DomainKind> RequiredDomains(IEnumerable<IFeatureRule> features) =>
        Enum.GetValues<DomainKind>().Where(RequiredParams(features).Requires).ToHashSet();
}
```

No `IMultiFeatureHandler` special case, no `FeatureOptions` parameter. Each feature already reports `Enabled` from its own config.

### 9. Config migration

The appsettings structure changes from flat boolean toggles to objects:

```json
// Before
{ "Njord:Features:Weather:Frost": true }
{ "Njord:Enrichment:Alerts": { "FrostThresholds": [0, -5, -15] } }

// After
{ "Njord:Features:Weather:Frost": { "Enabled": true, "Moderate": 2.0, "Warning": 0.0, "Severe": -5.0 } }
```

The old `Njord:Enrichment:Alerts` section is deleted. Each feature's section in `Njord:Features` contains both its toggle and its settings. A startup warning logs if the old `Njord:Enrichment:Alerts` section is present, naming the new paths.

### 9a. One `Njord.Features` project, folders by OutputKind, one folder per feature

The four handler projects (`Njord.Alerts`, `Njord.Scores`, `Njord.Insights`, `Njord.Advisories`) merge into
`Njord.Features` (+ `Njord.Features.Tests`). Layout: `<OutputKind>/<Feature>/` holding the rule, its config and its
result record; shared code per kind in `<OutputKind>/_Shared/` (`AlertWindow`, result base records, `Scoring` moved
back from Core, `SolarIrradianceBlend`); typed inputs in `Inputs/`. Namespaces follow folders. One
`AddFeatures(configuration)` registers everything explicitly.

Why: the project boundaries protected nothing (all four referenced only Core) but blocked reuse that the roadmap
needs (a charge-window score using `SolarIrradianceBlend`, a ventilation feature combining pollen and PM2.5 logic).
With typed inputs and one class per feature, a single project no longer becomes a monolith. OutputKind is the
first axis because it already shapes the result base records, the proto `oneof` and the shared helpers; the
category stays in the descriptor and config key. The old "domain projects are independent" rule becomes an
ArchUnit rule: a feature folder depends only on `_Shared`, `Inputs` and Core, never on another feature folder.

Alternatives: keep four projects (rejected: blocks reuse, vague names "Insights"/"Advisories"); folders by
category or domain (rejected: duplicates the descriptor, and many features read several domains).

### 10. Execution order

Phase 1: Core abstractions (`IFeatureRule`, `FeatureRule<T>`, `FeatureContext`, `FeatureInputs`, `FeatureResult` hierarchy, `RingBuffer`, `FeatureEngine`).
Phase 2: Per-feature config types and validators.
Phase 3: Feature implementations (Alerts → Scores → Insights → Advisories), replacing handlers/rules.
Phase 4: Pipeline integration (`ProjectionEngine` + `ForecastPipeline` use `FeatureEngine`).
Phase 5: Proto contract and gRPC mapping.
Phase 6: Delete old abstractions, update architecture tests, AGENTS.md, docs.
Phase 7: Final validation.

Within Phase 3, each feature project is migrated independently. The old `IDomainHandler` registrations are removed as each project is migrated. The build may temporarily break between phases 3 and 4 (handler interface removed but engine not yet wired); mitigated by keeping `IDomainHandler` as a deprecated adapter until Phase 6.

## Risks / Trade-offs

- **Build breakage during migration**: the handler interface removal and engine wiring must be coordinated. Mitigated by a shim that adapts `FeatureRule<T>` to `IDomainHandler` during transition, removed in Phase 6.
- **Proto field number allocation**: each feature gets a permanent field number in the `oneof`. Numbering must be planned upfront and never reused. Mitigated by reserving ranges per category (alerts 10-29, scores 50-59, etc.) and documenting the allocation in the proto file.
- **Config structure change**: users must update `appsettings.json` from flat booleans to objects. Mitigated by a startup warning that detects the old structure and names the new paths.
- **Test count**: all 233 handler tests are rewritten. Feature logic is preserved but test structure changes (no more TestKit for pure evaluation, just direct instantiation). The test count should remain comparable.
- **ha-njord incompatibility**: the proto contract is breaking. Mitigated by versioning (0.x) and noting the required ha-njord follow-up.
