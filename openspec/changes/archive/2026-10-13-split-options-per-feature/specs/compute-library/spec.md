## MODIFIED Requirements

### Requirement: Core references Compute for validation
`Njord.Core` SHALL hold a `ProjectReference` to `Njord.Compute` so that options-to-settings conversion can produce Compute's settings records. Each options validator SHALL live in `Njord.Core/Configuration/` next to the options type it validates.

#### Scenario: Core gains Compute reference
- **WHEN** `Njord.Core.csproj` is inspected
- **THEN** it SHALL include a `ProjectReference` to `Njord.Compute`

#### Scenario: Validators stay in Core
- **WHEN** `Njord.Core/Configuration/` is inspected
- **THEN** every `*Options` type has exactly one `*OptionsValidator` in the same folder

## ADDED Requirements

### Requirement: Compute receives settings records instead of options
`Njord.Compute` SHALL NOT contain options types or reference `Microsoft.Extensions.Options`. Computations SHALL receive immutable settings records (e.g. alert settings with typed severity thresholds, index settings with resolved preferences) that are built from validated options outside Compute. Options types (`AlertOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride`, `ConsensusOptions`, `TrendOptions`, `DerivedOptions`) SHALL live in `Njord.Core` under `Configuration/`, each next to its validator.

#### Scenario: Compute has no options
- **WHEN** `Njord.Compute` is inspected
- **THEN** it contains no `*Options` type and no `Configuration/` folder

#### Scenario: Settings records are immutable
- **WHEN** any settings record consumed by Compute is inspected
- **THEN** it is a record with init-only or positional properties and no array indexing by position for thresholds

## REMOVED Requirements

### Requirement: Compute-parameter Options live in Compute
**Reason**: Options types are configuration-binding concerns; Compute now consumes immutable settings records only.
**Migration**: Options types live in Njord.Core/Configuration/ next to their validators (see the added requirement).
