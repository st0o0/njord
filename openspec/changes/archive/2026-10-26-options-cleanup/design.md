## Context

Startup-resolved configuration is spread across three intermediate types and ad-hoc DI registrations:

- **FetchPlan** (singleton, 4 fields) — built in `DomainSetupContainer` via `FeatureResolution.BuildFetchPlan()`. Consumed by 6 domain clients, BudgetGuard, PollingOptionsValidator, and ServiceInfoMappingExtensions, but each consumer reads only 1-2 fields.
- **EffectiveWeatherModels** (nested in FetchPlan) — wraps `ImmutableArray<string>` with convenience accessors (`Primary`, `Count`, `Contains`, `Resolve`, `Ignored`). Every production consumer accesses it through `FetchPlan.Models`.
- **ServiceInfo** (singleton, 4 fields) — assembled in `ConfigSetupContainer` via a DI lambda pulling from 4 sources across two containers. Consumed only by 2 gRPC services, each reading a different subset.
- **IReadOnlySet\<DomainKind\> forwarding** — a DI registration in `DomainSetupContainer` that unwraps `FetchPlan.Domains` for PollWorker's constructor.

All four serve the same purpose: caching values derived from enabled features and weather options. The .NET OptionsBuilder pattern provides a framework-native way to achieve this.

## Goals / Non-Goals

**Goals:**
- Replace all intermediate types with a single `FeatureRequirementsOptions` using the OptionsBuilder pattern.
- Every consumer takes `IOptions<FeatureRequirementsOptions>`, consistent with the 61 existing `IOptions<T>` usages in the codebase.
- The computation is testable as a static method without DI.
- No behavioral changes: the same values are computed from the same inputs.

**Non-Goals:**
- Changing feature config registration (AddFeatures, IConfigSection, AddNjordOptions).
- Consolidating setup containers.
- Changing the Options pattern for non-feature options.
- Any polling, API, wire-format, or persistence changes.

## Decisions

### Decision 1: OptionsBuilder.Configure\<TDep\> over IConfigureOptions\<T\> class

**Chosen:** `AddOptions<T>().Configure<TDep1, TDep2>()` lambda delegating to a static method.

**Alternative:** A dedicated `IConfigureOptions<FeatureRequirementsOptions>` class (the David Fowl pattern). More ceremonial (separate class, separate registration) for the same result.

**Rationale:** The configure logic is 15 lines — a lambda calling a static method is the right size. The static method (`FeatureResolution.Configure`) keeps testability. If the logic grows, promoting to an `IConfigureOptions<T>` class is a one-line change.

### Decision 2: Static method on FeatureResolution, not on the options class

**Chosen:** `FeatureResolution.Configure(options, features, weather)` as a static method on the existing utility class.

**Alternative:** A static factory or `Configure` on `FeatureRequirementsOptions` itself.

**Rationale:** `FeatureResolution` already owns `WithBaseParams`, `RequiredParams`, and `RequiredDomains`. The configure method uses these utilities. Keeping it there follows the existing pattern and avoids the options class knowing about feature rules.

### Decision 3: WeatherCallWeight is not precomputed

**Chosen:** `FeatureRequirementsOptions` does not store `WeatherCallWeight`. `BudgetGuard` computes it from `Requirements` and `WeatherModels.Length`.

**Alternative:** Keep `WeatherCallWeight` on the options class.

**Rationale:** `WeatherCallWeight` is consumed by exactly one place (`BudgetGuard.Violation`). The formula is `CallWeight.Of(popcount(AllWeather), forecastDays) * modelCount` — BudgetGuard already knows the `CallWeight.Of` function and takes `WeatherOptions` for `ForecastDays`. Precomputing a single-consumer value on a shared options class is unnecessary coupling.

### Decision 4: ServiceInfo dissolved, not moved

**Chosen:** Delete `ServiceInfo`. WeatherGrpcService builds its `CatalogResult` from `NjordOptions` + `FeatureRequirementsOptions`. OpsGrpcService reads budget from `PollingOptions` and enrichments from `IEnumerable<IFeatureRule>`.

**Alternative:** Keep ServiceInfo but build it from `FeatureRequirementsOptions`.

**Rationale:** ServiceInfo has 2 consumers, each reading a disjoint subset. WeatherGrpcService reads only `Catalog`; OpsGrpcService reads `Budget` + `ActiveEnrichments`. There is no shared reader. The gRPC services can take their actual dependencies directly.

### Decision 5: Ignored-models warning moves to WeatherClient

**Chosen:** WeatherClient logs ignored models in its constructor.

**Alternative:** Log in the OptionsBuilder `Configure` lambda (requires a third dependency: `ILoggerFactory`).

**Rationale:** WeatherClient is the consumer that resolves and uses the models. It naturally sees the difference between configured and effective models. Keeping the Configure lambda at 2 dependencies (features + weather options) is cleaner.

### Decision 6: ComparisonFeatureName moves to ModelComparison

**Chosen:** The `"ModelComparison"` string constant moves from `EffectiveWeatherModels` to `ModelComparison.FeatureName`.

**Rationale:** The constant identifies the ModelComparison feature. It should live on the feature class, not on a model-resolution helper. `FeatureResolution.Configure` references it as `ModelComparison.FeatureName`.

## Risks / Trade-offs

- **Mutable options class** — `FeatureRequirementsOptions` has setters because the Options pattern requires a POCO with a parameterless constructor. The values are set once during `Configure` and never mutated afterward. This is standard Options-pattern usage, not a concurrency risk.
  Mitigation: None needed; this is how every `IOptions<T>` in the codebase works.

- **IConfigureOptions can run twice with ValidateOnStart** — [dotnet/runtime #71170](https://github.com/dotnet/runtime/issues/71170). The `Configure` lambda assigns properties (does not append), so it is idempotent.
  Mitigation: The lambda is idempotent by construction.

- **`.Value` on every access** — Consumers write `requirements.Value.Requirements.AllWeather` instead of `plan.Requirements.AllWeather`. One extra `.Value`, consistent with 61 existing usages.
  Mitigation: Store `requirements.Value` in a constructor field where the accessor would be verbose.

- **Broad file change count (~25 files)** — Mechanical: rename the injected type and adjust field access. No logic changes.
  Mitigation: Run all test projects after each batch of changes. The change is a pure refactoring — if it compiles and tests pass, it is correct.

## Migration Plan

No runtime migration needed. This is a compile-time refactoring:
1. Add `FeatureRequirementsOptions` and `FeatureResolution.Configure`.
2. Migrate consumers one project at a time (Core, OpenMeteo, Polling, Pipeline, Grpc, Host).
3. Delete `FetchPlan`, `EffectiveWeatherModels`, `ServiceInfo`, `ServiceInfoMappingExtensions`.
4. Update tests to use `IOptions<FeatureRequirementsOptions>` or `Options.Create(...)`.
5. Update AGENTS.md/CLAUDE.md references.

No persistence, proto, or wire-format changes. No rollback needed — this is a single-commit refactoring.
