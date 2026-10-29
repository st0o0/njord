## REMOVED Requirements

### Requirement: Feature requirements are resolved into a single options class
**Reason**: Replaced by `DomainRequirements` registered directly as `IOptions<DomainRequirements>`. The intermediate container added indirection without value; model fields were redundant with the Ensemble options, and the `Domains` list was a trivially derivable projection.
**Migration**: Consumers that injected `IOptions<FeatureRequirementsOptions>` inject `IOptions<DomainRequirements>` and access per-domain params directly (e.g. `.Value.AllForecast`). Consumers that read `.Domains` call `.Value.RequiredDomains()`. Consumers that read `.WeatherModels` or `.EnsembleModels` inject `IOptions<EnsembleOptions>` and read `.Value.Models`.

### Requirement: Configuration logic is testable without DI
**Reason**: The static `FeatureResolution.Configure` method is removed. The computation is inlined in the DI registration (PostConfigure). Testing the OR + base-params logic is done by testing `DomainRequirements.WithBaseParams()` directly, which is a pure instance method on the record.
**Migration**: Tests that called `FeatureResolution.Configure(options, rules, weather)` test `DomainRequirements.WithBaseParams()` and the `|` operator instead.

### Requirement: All consumers use IOptions for feature requirements
**Reason**: Consumers now use `IOptions<DomainRequirements>` (for parameter requirements) or `IOptions<EnsembleOptions>` / `IOptions<ForecastOptions>` (for model lists).
**Migration**: Replace `IOptions<FeatureRequirementsOptions>` with the appropriate direct dependency.

### Requirement: Ignored weather models are logged by the consumer
**Reason**: Retained but moved. The Ensemble client logs ignored models based on `EnsembleOptions.Models` and the `EnsembleParams.Models` flag, without depending on `FeatureRequirementsOptions`.
**Migration**: Logging moves from resolution-time filtering to client construction.

### Requirement: ComparisonFeatureName belongs to the feature
**Reason**: The string-based feature name check is eliminated entirely. The `EnsembleParams.Models` flag in `DomainRequirements` already carries the same signal; no name matching is needed.
**Migration**: Remove the constant from both `FeatureResolution` and `ModelComparison`. The `EnsembleClient` uses `AllEnsemble.HasFlag(EnsembleParams.Models)` which it already does today.
