## REMOVED Requirements

### Requirement: Single startup-computed fetch plan
**Reason**: Replaced by `FeatureRequirementsOptions` registered through the .NET OptionsBuilder pattern. The same data (domains, requirements, models, call weight) is now exposed as an options class consumed via `IOptions<FeatureRequirementsOptions>`.
**Migration**: All consumers that depended on `FetchPlan` SHALL take `IOptions<FeatureRequirementsOptions>` instead. Call weight is no longer precomputed; `BudgetGuard` SHALL compute it from `Requirements` and `WeatherModels.Length`.

### Requirement: FetchPlan replaces separate derived registrations
**Reason**: The consolidation goal is now fulfilled by `FeatureRequirementsOptions`. No `FetchPlan`, `EffectiveWeatherModels`, `IReadOnlySet<DomainKind>` forwarding, or `ServiceInfo` registrations SHALL exist.
**Migration**: Consumers take `IOptions<FeatureRequirementsOptions>` for requirements and domains, `IOptions<PollingOptions>` for budget limits, and `IEnumerable<IFeatureRule>` for enrichment listings.

### Requirement: Ignored weather models are logged at build time
**Reason**: The warning moves from FetchPlan construction to the weather client constructor, which is the consumer that resolves and uses the models.
**Migration**: The weather client SHALL log ignored models at construction time.
