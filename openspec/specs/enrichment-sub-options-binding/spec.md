# enrichment-sub-options-binding Specification

## Purpose

Binds nested EnrichmentOptions sub-types (AlertOptions, IndexOptions,
DerivedOptions, TrendOptions, ConsensusOptions) individually in DI so that
`IOptions<T>` resolves to config-populated instances, not empty defaults.
## Requirements
### Requirement: Each EnrichmentOptions sub-type is individually bound in DI

`AlertOptions`, `IndexOptions`, `DerivedOptions`, `TrendOptions`, and
`ConsensusOptions` SHALL each be registered exactly once via
`AddOptions<T>().BindConfiguration(T.SectionName).ValidateOnStart()` so that
`IOptions<T>` resolves to config-populated, validated instances. The section paths
are unchanged: `Njord:Enrichment:Alerts`, `Njord:Enrichment:Indices`,
`Njord:Enrichment:Derived`, `Njord:Enrichment:Trends`, `Njord:Enrichment:Consensus`.
No aggregate `EnrichmentOptions` type SHALL be bound.

#### Scenario: AlertOptions resolves from config

- **WHEN** configuration contains `Njord:Enrichment:Alerts:Enabled = false`
- **THEN** `IOptions<AlertOptions>.Value.Enabled` SHALL equal `false`

#### Scenario: IndexOptions resolves from config

- **WHEN** configuration contains `Njord:Enrichment:Indices:Enabled = true`
- **THEN** `IOptions<IndexOptions>.Value.Enabled` SHALL equal `true`

#### Scenario: TrendOptions resolves from config

- **WHEN** configuration contains `Njord:Enrichment:Trends:Enabled = false`
- **THEN** `IOptions<TrendOptions>.Value.Enabled` SHALL equal `false`

#### Scenario: DerivedOptions resolves from config

- **WHEN** configuration contains `Njord:Enrichment:Derived:Enabled = true`
- **THEN** `IOptions<DerivedOptions>.Value.Enabled` SHALL equal `true`

#### Scenario: ConsensusOptions resolves from config

- **WHEN** configuration contains `Njord:Enrichment:Consensus:MinQuorum = 3`
- **THEN** `IOptions<ConsensusOptions>.Value.MinQuorum` SHALL equal `3`

#### Scenario: No aggregate binding

- **WHEN** the service provider is inspected
- **THEN** no `IOptions<EnrichmentOptions>` registration exists

