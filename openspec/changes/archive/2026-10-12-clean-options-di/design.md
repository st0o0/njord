## Context

The analysis feature analyzers are registered via per-feature
`ServiceCollectionExtensions.AddXxx()` methods called from
`AnalysisSetupContainer`. Today, `AnalysisSetupContainer` binds
`EnrichmentOptions` as a whole but never binds the nested sub-types
individually. The result:

- `AlertAnalyzer` takes `IOptions<AlertOptions>` — gets empty defaults.
- `IndexAnalyzer` takes raw `IndexOptions` + `IEnumerable<string>` — unresolvable.
- `TrendAnalyzer` takes `bool enabled = true` — ignores `TrendOptions.Enabled`.
- `DerivedAnalyzer` is built by a manual factory that extracts
  `NjordOptions.Horizons` into a primitive list.
- `MqttNetPublisher` takes raw `MqttOptions` via a factory that unwraps
  `IOptions<MqttOptions>.Value`.
- `MqttSetupContainer` and `CoreSetupContainer` read `Mqtt:Enabled` from raw
  `IConfiguration` instead of using the bound `MqttOptions`.

Sub-option types live in two namespaces: `DerivedOptions`, `TrendOptions`,
`ConsensusOptions` in `Njord.Core.Configuration`; `AlertOptions`, `IndexOptions`
in `Njord.Compute.Configuration`.

## Goals / Non-Goals

**Goals:**
- Every `EnrichmentOptions` sub-type individually bound so `IOptions<T>` works.
- All analyzers use `IOptions<T>` constructor injection — no raw types, no
  extracted primitives, no default booleans.
- `MqttNetPublisher` takes `IOptions<MqttOptions>`.
- Setup containers use bound options for the `Mqtt:Enabled` check.

**Non-Goals:**
- Moving sub-option types between assemblies.
- Adding `IOptionsMonitor` / hot-reload to analyzers (they are singletons).
- Changing the admin gRPC mutation path.
- Adding new validators for the sub-options (existing validators on
  `EnrichmentOptions` already cover `ConsensusOptions` and `IndexOptions`).

## Decisions

### 1. Bind sub-options in AnalysisSetupContainer, not in each feature library

**Decision:** Add the five `AddOptions<T>().Bind()` calls to
`AnalysisSetupContainer.SetupServices`, right after the existing
`EnrichmentOptions` binding.

**Why not in each `AddXxx()` extension method?** The feature libraries
(`Njord.Alerts`, `Njord.Indices`, etc.) don't receive `IConfiguration` —
their `AddXxx()` methods take only `IServiceCollection`. Passing
`IConfiguration` to each would widen the API surface. The setup container
already has `IConfiguration` and already owns the `EnrichmentOptions` binding,
so co-locating the sub-bindings there is natural.

**Alternative considered:** Have each `AddXxx()` method call
`services.AddOptions<T>().BindConfiguration("Njord:Enrichment:Xxx")`. This
works without `IConfiguration` but hard-codes the section path string in
each feature library. Rejected because the section path is an application
concern, not a library concern.

### 2. Analyzer constructors take IOptions of T, resolved via auto-DI

**Decision:** Refactor each analyzer constructor:

| Analyzer | Current | After |
|---|---|---|
| `AlertAnalyzer` | `IOptions<AlertOptions>` | no change (already correct, now gets real values) |
| `IndexAnalyzer` | `IndexOptions`, `IEnumerable<string>`, `bool` | `IndexComputer`, `IOptions<IndexOptions>`, `IOptions<NjordOptions>` |
| `TrendAnalyzer` | `TrendComputer`, `bool enabled` | `TrendComputer`, `IOptions<TrendOptions>` |
| `DerivedAnalyzer` | `DerivedResultComputer`, `IReadOnlyList<int>` | `DerivedResultComputer`, `IOptions<NjordOptions>` |

All registrations become plain `services.AddSingleton<T>()` — the DI container
auto-resolves `IOptions<T>`. The manual factory in `DerivedAnalyzer`'s
`AddDerived()` is removed.

**Why `IOptions<NjordOptions>` for horizons/locations instead of a new options
type?** Horizons and location names are top-level `NjordOptions` properties
used by exactly two analyzers. Extracting them into a dedicated options type
would add indirection for no consumer benefit.

### 3. MqttNetPublisher takes IOptions of MqttOptions

**Decision:** Change the constructor from `MqttOptions options` to
`IOptions<MqttOptions> options`. Remove the manual factory in
`MqttSetupContainer` and register via `services.TryAddSingleton<MqttNetPublisher>()`.

### 4. Replace raw IConfiguration reads with options

**Decision:** In `MqttSetupContainer` and `CoreSetupContainer`, replace:
```csharp
var mqttEnabled = configuration.GetSection("Njord:Mqtt").GetValue("Enabled", false);
```
with building a temporary `MqttOptions` from the already-bound section:
```csharp
var mqttEnabled = configuration.GetSection(MqttOptions.SectionName).Get<MqttOptions>()?.Enabled ?? false;
```

This reads from the same config section as the bound options. We can't use
`IOptions<MqttOptions>` here because the setup container runs during DI
registration, before the service provider is built.

## Risks / Trade-offs

- **[Risk] Tests construct analyzers directly with `new`** → Tests need
  updated constructor calls. Mitigation: each test project's specs will need
  `Options.Create(new XxxOptions { ... })` wrappers. Straightforward but
  touches many files.
- **[Risk] IndexAnalyzer location names from IOptions at resolve time** →
  `NjordOptions.Locations` must be populated before `IndexAnalyzer` resolves.
  Mitigation: Options are bound from config at startup before any singleton
  resolution — this is guaranteed by the hosting model.
