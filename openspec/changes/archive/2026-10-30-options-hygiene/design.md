## Context

`NjordOptions` holds `IList<LocationOptions>` with nullable coordinates for the config binder.
After `NjordOptionsValidator` guarantees non-null values, every consumer still works with
`LocationOptions` and calls `GetValueOrDefault()` to build `Location`. Two call sites do this
today: `PollManagerConfig.Create` and `CatalogMessageMappingExtensions.ToCatalogResult`.

Three derived options (`DomainRequirements`, `ActiveModels`, `DomainCallConfig`) are registered
as raw `IOptions<T>` singletons via `Options.Create(...)` in `DomainSetupContainer`, bypassing
the Options pipeline.

## Goals / Non-Goals

**Goals:**
- Eliminate ad-hoc `LocationOptions -> Location` conversions after validation.
- Register all derived options through the Options pipeline (`AddOptions`,
  `IConfigureOptions`, `IOptionsFactory`).
- Keep `LocationOptions` as the config-binder target (nullable doubles stay).

**Non-Goals:**
- Changing the `Njord__Locations__0__Name` environment variable layout.
- Making options hot-reloadable (`IOptionsMonitor`).
- Touching persistence records or gRPC contracts.

## Decisions

### 1. `PostConfigure<NjordOptions>` builds `ResolvedLocations`

Add `IReadOnlyList<Location> ResolvedLocations { get; set; } = []` to `NjordOptions`.
A `PostConfigure` registered in `AddNjordOptions` converts each `LocationOptions` after
validation. `FindLocation` returns `Location?` from `ResolvedLocations`.

**Why not a separate options class?** `NjordOptions` already owns the locations; adding a
second type for the same data doubles the injection surface for no gain.

### 2. `IOptionsFactory<DomainRequirements>` for requirement aggregation

A `DomainRequirementsFactory : IOptionsFactory<DomainRequirements>` takes
`IEnumerable<IFeatureRule>`, aggregates enabled rules with `|`, calls `WithBaseParams()`.

**Why factory, not PostConfigure?** `DomainRequirements` is a positional record with
`init`-only properties. PostConfigure needs mutation of an existing instance; the factory
creates the instance, so the record stays immutable. This also matches the semantics: the
value is computed from scratch, not modified.

**Alternative considered:** Making `DomainRequirements` mutable with `{ get; set; }`.
Rejected because the `|` operator and immutability are load-bearing for domain correctness
and the factory fits naturally.

### 3. `AddOptions<ActiveModels>().Configure<A, B>()` for model resolution

Change `ActiveModels` properties from `init` to `set`. Only two deps (ForecastOptions,
EnsembleOptions), fits the generic `Configure` overload.

### 4. `IConfigureOptions<DomainCallConfig>` class for call config

`ConfigureDomainCallConfig` takes 7 `IOptions<*>` via constructor DI. Convert
`DomainCallConfig` from positional record to non-positional with `{ get; set; }` and
matching defaults.

**Why a class, not inline Configure?** The generic `Configure<T1..T5>` overload accepts at
most 5 type parameters; 7 domain-options dependencies need a full class.

### 5. `PollManagerConfig.Create` takes `IReadOnlyList<Location>`

Downstream of `ResolvedLocations`. Removes the last `GetValueOrDefault()` call site.

## Risks / Trade-offs

- [ActiveModels / DomainCallConfig become mutable] The `set` properties allow mutation
  after construction. Mitigated: the Options pipeline runs once at startup; no runtime code
  mutates these. Convention-enforced, same as every other Options class.
- [DomainRequirementsFactory replaces IOptions singleton] Consumers are unchanged
  (`IOptions<DomainRequirements>`), but the instantiation path differs. Mitigated: the
  factory runs the same aggregation logic; existing tests cover the resolution.
