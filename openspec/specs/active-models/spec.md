# active-models Specification

## Purpose
TBD - created by archiving change active-models. Update Purpose after archive.
## Requirements
### Requirement: ActiveModels carries resolved model identifiers
The system SHALL provide a read-only `ActiveModels` record in `Njord.Core` with three
properties: `PrimaryForecastModel` (string), `ForecastModels` (read-only string list),
and `EnsembleModels` (read-only string list). It SHALL be registered as
`IOptions<ActiveModels>`.

#### Scenario: Default configuration (empty Forecast.Models)
- **WHEN** `Njord:Forecast:Models` is not configured and `Njord:Ensemble:Models` resolves to `["ecmwf_ifs025_ensemble_mean"]`
- **THEN** `PrimaryForecastModel` SHALL be `"best_match"`, `ForecastModels` SHALL be empty, and `EnsembleModels` SHALL be `["ecmwf_ifs025_ensemble_mean"]`

#### Scenario: Explicit forecast models
- **WHEN** `Njord:Forecast:Models` is `["icon_d2", "icon_eu"]`
- **THEN** `PrimaryForecastModel` SHALL be `"icon_d2"`, `ForecastModels` SHALL be `["icon_d2", "icon_eu"]`

#### Scenario: Multiple ensemble models
- **WHEN** `Njord:Ensemble:Models` is `["ecmwf_ifs025_ensemble_mean", "ecmwf_aifs025_ensemble_mean"]`
- **THEN** `EnsembleModels` SHALL contain both model identifiers

### Requirement: Host builds ActiveModels via PostConfigure
The Host SHALL register `ActiveModels` using `PostConfigure` that reads the resolved
`ForecastOptions` and `EnsembleOptions` values. No other project SHALL reference
`EnsembleOptions` or `ForecastOptions` to obtain model lists.

#### Scenario: PostConfigure runs after option defaults
- **WHEN** `EnsembleOptions.ApplyDefaults()` has run (filling empty Models with the default model)
- **THEN** `ActiveModels.EnsembleModels` SHALL reflect the post-default value

#### Scenario: Grpc does not reference OpenMeteo
- **WHEN** the solution is inspected
- **THEN** `Njord.Grpc.csproj` SHALL NOT have a ProjectReference to `Njord.OpenMeteo`

