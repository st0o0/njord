## ADDED Requirements

### Requirement: Feature requirements are resolved into a single options class
The system SHALL compute the resolved feature requirements at startup and expose them as `FeatureRequirementsOptions`, registered through the .NET OptionsBuilder pattern (`AddOptions<T>().Configure<TDep>()`). The options class SHALL hold:
- the per-domain parameter requirements (union of enabled features' requirements, with base parameters applied),
- the list of domains that must be polled (derived from the requirements),
- the weather ensemble model IDs to fetch (primary only, or all configured models when the model-comparison feature is enabled),
- the primary weather model (first in the model list).

The options class SHALL be a POCO with a public parameterless constructor and read-write properties, following the .NET Options pattern conventions.

#### Scenario: Options are resolved from enabled features
- **WHEN** the host starts with Frost (Weather) and Pollen (AirQuality) enabled
- **THEN** `FeatureRequirementsOptions.Requirements` SHALL contain the union of both features' parameters with base params, `Domains` SHALL contain `{Weather, AirQuality}`, and `WeatherModels` SHALL contain the primary model

#### Scenario: Model comparison enables all configured models
- **WHEN** the host starts with ModelComparison enabled and three models configured
- **THEN** `FeatureRequirementsOptions.WeatherModels` SHALL contain all three model IDs

#### Scenario: No enabled features
- **WHEN** no feature rules are enabled
- **THEN** `Requirements` SHALL be default, `Domains` SHALL be empty, and `WeatherModels` SHALL contain only the default model

#### Scenario: ValidateOnStart triggers resolution
- **WHEN** the host builds its service provider
- **THEN** `FeatureRequirementsOptions` SHALL be resolved and validated before the host starts accepting requests

### Requirement: Configuration logic is testable without DI
The computation that populates `FeatureRequirementsOptions` SHALL be a static method on `FeatureResolution`, accepting the options instance, the feature rules, and the weather options as parameters. Tests SHALL be able to call this method directly without a service provider.

#### Scenario: Static configure method
- **WHEN** a test calls the static configure method with two feature rules
- **THEN** the options instance SHALL be populated with the union of both rules' requirements

### Requirement: All consumers use IOptions for feature requirements
Every consumer that previously depended on `FetchPlan`, `EffectiveWeatherModels`, `ServiceInfo`, or the `IReadOnlySet<DomainKind>` forwarding SHALL take `IOptions<FeatureRequirementsOptions>` instead. No other DI registration SHALL exist for derived feature-requirement values.

#### Scenario: Domain client reads its requirements
- **WHEN** a domain client is constructed
- **THEN** it SHALL obtain its API variable list from `IOptions<FeatureRequirementsOptions>.Value.Requirements`

#### Scenario: PollWorker reads domains
- **WHEN** a poll worker is constructed
- **THEN** it SHALL obtain the domain set from `IOptions<FeatureRequirementsOptions>.Value.Domains`

#### Scenario: BudgetGuard reads requirements and models
- **WHEN** the budget projection runs at startup
- **THEN** it SHALL read `Requirements` and `WeatherModels.Length` from `FeatureRequirementsOptions`

### Requirement: Ignored weather models are logged by the consumer
When the operator configures weather models that are not fetched (because model comparison is disabled), the weather client SHALL log a warning naming each ignored model at construction time.

#### Scenario: Models configured but comparison disabled
- **WHEN** three models are configured and ModelComparison is disabled
- **THEN** the weather client SHALL log a warning naming the two ignored models

### Requirement: ComparisonFeatureName belongs to the feature
The string constant identifying the model-comparison feature by name SHALL be defined on the `ModelComparison` rule class, not on a model-resolution type. `FeatureResolution` SHALL reference this constant when checking whether model comparison is enabled.

#### Scenario: Constant location
- **WHEN** `FeatureResolution.Configure` checks for the model-comparison feature
- **THEN** it SHALL use the constant defined on the `ModelComparison` class
