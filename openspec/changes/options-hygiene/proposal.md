# Options Hygiene

## Problem

Two Options anti-patterns have accumulated:

### 1. LocationOptions / Location split

`NjordOptions.Locations` holds `IList<LocationOptions>` with nullable `double?` coordinates.
After validation these are guaranteed non-null, yet every consumer must convert ad-hoc with
`GetValueOrDefault()` (in `CatalogMessageMappingExtensions` and `PollManagerConfig.Create`).
`NjordOptions.FindLocation` returns `LocationOptions?` instead of `Location`, spreading nullable
doubles into call sites that already know the data is valid.

### 2. Manual `IOptions<T>` singletons for derived options

`DomainSetupContainer` registers three derived options types via
`services.AddSingleton<IOptions<T>>(sp => Options.Create(...))`. This bypasses the Options
pipeline: no `ValidateOnStart`, no `PostConfigure`, no `IConfigureOptions<T>` composition.

```
DomainRequirements    -- aggregated from IFeatureRule instances
ActiveModels          -- derived from ForecastOptions + EnsembleOptions
DomainCallConfig      -- derived from 7 domain options
```

## Solution

### LocationOptions cleanup

- Add `NjordOptions.ResolvedLocations: IReadOnlyList<Location>` populated by a `PostConfigure`
  that converts each validated `LocationOptions` to `Location` once.
- Change `NjordOptions.FindLocation(name)` to return `Location?` from `ResolvedLocations`.
- Change `PollManagerConfig.Create` to accept `IReadOnlyList<Location>` instead of
  `IList<LocationOptions>`.
- Change `CatalogMessageMappingExtensions.ToCatalogResult` to use `ResolvedLocations`.
- Remove all `GetValueOrDefault()` conversions of `LocationOptions` coordinates.

### Derived options via proper Options-stack

**DomainRequirements** -- `IOptionsFactory<DomainRequirements>`: a custom factory that
aggregates enabled feature rules and calls `WithBaseParams()`. The record stays positional and
immutable. Consumers keep injecting `IOptions<DomainRequirements>` unchanged.

**ActiveModels** -- `AddOptions<ActiveModels>().Configure<IOptions<ForecastOptions>,
IOptions<EnsembleOptions>>(...)`: change `init` to `set` on the three properties. Only two
dependencies, fits in the generic `Configure` overload.

**DomainCallConfig** -- `IConfigureOptions<DomainCallConfig>` class: seven options dependencies
exceed the 5-type-parameter limit of the generic `Configure`. Convert from positional record to
non-positional with `{ get; set; }` and default values.

### Non-goals

- Changing the `Njord__Locations__0__Name` environment-variable layout to a dictionary style.
- Making options hot-reloadable (`IOptionsMonitor`).
- Touching `LocationOptions` itself (it stays as the config-binder target with nullable doubles).

## Impact

- No API-budget impact (no polling changes).
- No persistence-record changes.
- No gRPC contract changes.
- Breaking: test code that constructs `PollManagerConfig.Create(IList<LocationOptions>)` changes
  to `PollManagerConfig.Create(IReadOnlyList<Location>)`.
