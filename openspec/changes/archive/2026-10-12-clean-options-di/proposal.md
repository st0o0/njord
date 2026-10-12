## Why

The analysis feature analyzers (`AlertAnalyzer`, `IndexAnalyzer`, `TrendAnalyzer`,
`DerivedAnalyzer`) have inconsistent and partly broken DI wiring. The nested
sub-options inside `EnrichmentOptions` (`AlertOptions`, `IndexOptions`,
`DerivedOptions`, `TrendOptions`, `ConsensusOptions`) are never individually
bound via `AddOptions<T>().Bind()`, so consumers that request `IOptions<T>` get
empty defaults, consumers that take the raw type can't resolve at all, and
constructor-default booleans silently override config values. Additionally,
`MqttNetPublisher` takes raw `MqttOptions` instead of `IOptions<MqttOptions>`,
and two setup containers read `Mqtt:Enabled` from raw `IConfiguration` instead
of the bound options.

## What Changes

- Bind each `EnrichmentOptions` sub-type (`AlertOptions`, `IndexOptions`,
  `DerivedOptions`, `TrendOptions`, `ConsensusOptions`) individually via
  `AddOptions<T>().Bind()` in the analysis setup container.
- Refactor `IndexAnalyzer` to take `IOptions<IndexOptions>` and
  `IOptions<NjordOptions>` (for location names) instead of raw types.
- Refactor `TrendAnalyzer` to take `IOptions<TrendOptions>` instead of a
  `bool enabled` constructor parameter.
- Refactor `DerivedAnalyzer` to take `IOptions<NjordOptions>` (for horizons)
  instead of a manual factory extracting `IReadOnlyList<int>`.
- Verify `AlertAnalyzer` works correctly once `AlertOptions` is bound (it
  already takes `IOptions<AlertOptions>`).
- Refactor `MqttNetPublisher` to take `IOptions<MqttOptions>` instead of raw
  `MqttOptions`.
- Replace raw `IConfiguration.GetValue("Enabled")` reads in
  `MqttSetupContainer` and `CoreSetupContainer` with
  `IOptions<MqttOptions>.Value.Enabled`.

## Non-goals

- Changing the admin gRPC mutation path (`NjordOptions` as a unit) — that stays
  as-is per the `standalone-sub-options` spec.
- Extracting `EnrichmentOptions` sub-types out of `EnrichmentOptions` as
  top-level config sections — they stay nested, just dual-bound.
- Adding `IOptionsMonitor` / hot-reload support to analyzers — they are
  singletons resolved once at startup.

## Capabilities

### New Capabilities

- `enrichment-sub-options-binding`: Bind nested EnrichmentOptions sub-types
  individually in DI so analyzers receive correct config values.
- `analyzer-di-cleanup`: Refactor analyzer constructors to use `IOptions<T>`
  consistently instead of raw types, primitives, or constructor-default booleans.
- `mqtt-options-consistency`: Make MqttNetPublisher and setup containers use
  bound options instead of raw config reads.

### Modified Capabilities

_(none — no existing spec-level requirements change)_

## Impact

- `Njord.Alerts/AlertAnalyzer.cs` — no code change, but now receives real config.
- `Njord.Indices/IndexAnalyzer.cs` — constructor signature change.
- `Njord.Trends/TrendAnalyzer.cs` — constructor signature change.
- `Njord.Derived/DerivedAnalyzer.cs` — constructor signature change.
- `Njord.Derived/ServiceCollectionExtensions.cs` — remove manual factory.
- `Njord.Mqtt/Transport/MqttNetPublisher.cs` — constructor signature change.
- `Njord.Mqtt/Configuration/MqttSetupContainer.cs` — use options instead of raw config.
- `Njord/Configuration/CoreSetupContainer.cs` — use options instead of raw config.
- `Njord/Configuration/AnalysisSetupContainer.cs` — add sub-option bindings.
- All test projects for the above — update construction/DI setup.
