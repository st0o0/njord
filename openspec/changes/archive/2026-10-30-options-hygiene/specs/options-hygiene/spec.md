## ADDED Requirements

### Requirement: NjordOptions resolves locations after validation
`NjordOptions` SHALL expose a `ResolvedLocations` property containing validated `Location`
values. `FindLocation` SHALL return `Location?` from the resolved list.

#### Scenario: Resolved locations available after startup
- **WHEN** the host starts with valid location configuration
- **THEN** `IOptions<NjordOptions>.Value.ResolvedLocations` contains one `Location` per
  configured `LocationOptions` with non-nullable coordinates

#### Scenario: FindLocation returns Location
- **WHEN** a consumer calls `FindLocation("Lucerne")` on a validated `NjordOptions`
- **THEN** the result is a `Location?` (not `LocationOptions?`) with the matching name,
  latitude and longitude

#### Scenario: FindLocation returns null for unknown name
- **WHEN** a consumer calls `FindLocation("Unknown")`
- **THEN** the result is `null`

### Requirement: DomainRequirements resolved via Options pipeline
`DomainRequirements` SHALL be provided through the Options pipeline using a custom factory,
not as a manually registered `IOptions<T>` singleton.

#### Scenario: Requirements aggregate enabled features
- **WHEN** the host starts with enabled feature rules
- **THEN** `IOptions<DomainRequirements>.Value` equals the OR-aggregate of all enabled
  rules' requirements with base params applied

#### Scenario: Disabled features excluded
- **WHEN** a feature rule is disabled
- **THEN** its requirements are not included in the resolved `DomainRequirements`

### Requirement: ActiveModels resolved via Options pipeline
`ActiveModels` SHALL be configured through `AddOptions` with dependencies on
`ForecastOptions` and `EnsembleOptions`.

#### Scenario: Models derived from forecast and ensemble options
- **WHEN** the host starts with `ForecastOptions.Models = ["icon_d2", "icon_eu"]` and
  `EnsembleOptions.Models = ["ecmwf_ifs025_ensemble_mean"]`
- **THEN** `IOptions<ActiveModels>.Value.PrimaryForecastModel` is `"icon_d2"`,
  `ForecastModels` is `["icon_d2", "icon_eu"]`, and `EnsembleModels` is
  `["ecmwf_ifs025_ensemble_mean"]`

#### Scenario: Empty forecast models default to best_match
- **WHEN** `ForecastOptions.Models` is empty
- **THEN** `PrimaryForecastModel` is `"best_match"`

### Requirement: DomainCallConfig resolved via Options pipeline
`DomainCallConfig` SHALL be configured through `IConfigureOptions<DomainCallConfig>` with
dependencies on all domain options.

#### Scenario: Call config derived from domain options
- **WHEN** the host starts with configured domain options
- **THEN** `IOptions<DomainCallConfig>.Value` reflects the days, model counts, and flags
  from each domain's options

### Requirement: No ad-hoc nullable coordinate conversions
No production code outside `NjordOptions` SHALL call `GetValueOrDefault()` on
`LocationOptions.Latitude` or `LocationOptions.Longitude`.

#### Scenario: PollManagerConfig uses resolved locations
- **WHEN** `PollManagerConfig.Create` is called
- **THEN** it accepts `IReadOnlyList<Location>` (non-nullable coordinates), not
  `IList<LocationOptions>`

#### Scenario: Catalog mapping uses resolved locations
- **WHEN** the catalog is built for gRPC
- **THEN** it reads from `NjordOptions.ResolvedLocations`, not from `Locations` with
  `GetValueOrDefault()`
