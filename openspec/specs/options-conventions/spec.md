# options-conventions Specification

## Purpose
TBD - created by archiving change options-parity-with-funkarr. Update Purpose after archive.
## Requirements
### Requirement: Options are registered through one helper
`Njord.Core` SHALL provide `AddNjordOptions<T>()` and `AddNjordOptions<T, TValidator>()` for types implementing `IConfigSection` (static abstract `SectionName`); they bind the section, register the explicit `IValidateOptions<T>` and enable `ValidateOnStart`. Setup containers SHALL NOT repeat these steps by hand.

#### Scenario: Helper registers validation
- **WHEN** an options type is added with `AddNjordOptions<T>` and its value is invalid
- **THEN** the host fails at start with the validator's messages

### Requirement: Legacy root-level keys fail loudly
When configuration or the environment contains `Njord:PollInterval`, `Njord:DiscoveryInterval`, `Njord:BudgetOverride`, `Njord:OpenMeteoBaseUrl` or `Njord:PersistencePath`, startup validation SHALL fail with a message naming the old key and its new location. The override file SHALL be migrated in place when it contains old keys: the values move to the new sections, a warning is logged, and a corrupt file keeps its existing resilience behaviour.

#### Scenario: Old environment variable
- **WHEN** `Njord__PollInterval=00:30:00` is set
- **THEN** startup fails with a message that names `Njord:PollInterval` and `Njord:Polling:PollInterval`

#### Scenario: Old override file
- **WHEN** the override file contains `"PollInterval": "00:45:00"` at the `Njord` level
- **THEN** the effective value is read from `Njord:Polling:PollInterval` and the file is rewritten with the new key on the next write

### Requirement: No Effective* workaround properties
Options classes SHALL NOT use nullable properties with computed Effective* fallback properties. Default values SHALL be applied via PostConfigure<T> registrations so that IOptions<T>.Value always carries the resolved value.

#### Scenario: ForecastOptions exposes Models directly
- **WHEN** IOptions<ForecastOptions>.Value is accessed
- **THEN** Models is a non-nullable string[] (never null at runtime)
- **THEN** EffectiveModels and PrimaryModel properties do not exist
- **THEN** the first element of Models is the primary model

#### Scenario: Feature config Thresholds are non-nullable after startup
- **WHEN** no Thresholds are configured for a feature
- **THEN** IOptions<TConfig>.Value.Thresholds returns the feature's default threshold array

### Requirement: ForecastOptions.Model migration shim removed
ForecastOptions SHALL NOT have a `Model` (singular) property. The 0.4.x migration path is in the changelog, not in runtime code.

#### Scenario: ForecastOptions.Model does not exist
- **WHEN** ForecastOptions is inspected
- **THEN** there is no `Model` (singular) property

### Requirement: Single source of truth for SolarYield PeakKw
PeakKw and SystemEfficiency SHALL be configured only through SolarYieldOptions (section Njord:SolarYield). SolarYieldConfig SHALL NOT duplicate these properties; the host copies them via PostConfigure.

#### Scenario: Single source of truth for PeakKw
- **WHEN** the SolarYield feature computes yield
- **THEN** it reads PeakKw from SolarYieldOptions only (section Njord:SolarYield)

### Requirement: Example defaults match appsettings.json
Every `# Default:` value in `docker-compose.example.yml` SHALL equal the value of the same key in `appsettings.json` (or the code default for list-valued options). A spec SHALL fail on any mismatch.

#### Scenario: Stale default in the compose example
- **WHEN** the compose example says `# Default: 30` and `appsettings.json` holds 60 minutes
- **THEN** the spec fails and names the variable

