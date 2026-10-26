# fetch-plan Specification

## Purpose
A single startup-computed fetch plan is the sole source of derived fetch configuration (domains, requirements, models, call weight).

## Requirements
### Requirement: Single startup-computed fetch plan
The system SHALL compute a single `FetchPlan` at startup from the enabled feature rules and
the weather/flood/seasonal/satellite options. The plan SHALL expose:
- the set of required domains,
- the per-domain parameter requirements (with base parameters applied),
- the effective weather models (primary and, when model comparison is enabled, secondary),
- the precomputed weather call weight.

The plan SHALL be registered as a singleton and SHALL be the sole source of derived fetch
configuration for all consumers.

#### Scenario: All derived values from one computation
- **WHEN** the host starts with three enabled features requiring Weather and one requiring AirQuality
- **THEN** `FetchPlan` SHALL contain exactly `{Weather, AirQuality}` as domains, the union of
  all required parameters with base parameters applied, the effective models, and the weather
  call weight computed as `ceil(variables / 10)`

#### Scenario: No enabled features
- **WHEN** no feature rules are enabled
- **THEN** `FetchPlan` SHALL contain an empty domain set, empty requirements, only the primary
  weather model, and a call weight of zero

### Requirement: FetchPlan replaces separate derived registrations
The three separate DI registrations (`IReadOnlySet<DomainKind>`, `EffectiveWeatherModels`,
`ResolvedRequirements`) SHALL be removed. All consumers that previously depended on any of
those three types SHALL depend on `FetchPlan` instead.

#### Scenario: BudgetGuard uses FetchPlan
- **WHEN** `BudgetGuard` validates the projected budget at startup
- **THEN** it SHALL read domains, call weight and model count from `FetchPlan` instead of
  assembling them from separate registrations

#### Scenario: Clients use FetchPlan
- **WHEN** a domain client resolves which API variables to request
- **THEN** it SHALL obtain its domain's requirements from `FetchPlan`

#### Scenario: PollWorker uses FetchPlan
- **WHEN** the poll worker decides which domains to poll for a location
- **THEN** it SHALL read the required domain set from `FetchPlan`

### Requirement: Ignored weather models are logged at build time
When the operator configures weather models that are not in the known catalog,
`FetchPlan` construction SHALL log a warning naming each ignored model. This logging
SHALL happen during plan construction, not inside a DI factory lambda.

#### Scenario: Unknown model configured
- **WHEN** `Njord:Weather:Models` contains `"unknown_model"` alongside a valid model
- **THEN** the build step SHALL log a warning naming `unknown_model` and the plan SHALL
  contain only the valid model
