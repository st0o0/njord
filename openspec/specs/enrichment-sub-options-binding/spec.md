# enrichment-sub-options-binding Specification

## Purpose

Binds nested EnrichmentOptions sub-types (AlertOptions, IndexOptions,
DerivedOptions, TrendOptions, ConsensusOptions) individually in DI so that
`IOptions<T>` resolves to config-populated instances, not empty defaults.

## Requirements

### Requirement: Each EnrichmentOptions sub-type is individually bound in DI

`AlertOptions`, `IndexOptions`, `DerivedOptions`, `TrendOptions`, and
`ConsensusOptions` SHALL each be registered via
`AddOptions<T>().Bind(enrichmentSection.GetSection("<name>"))` so that
`IOptions<T>` resolves to config-populated instances, not empty defaults.
The binding section path follows the nesting:
`Njord:Enrichment:Alerts`, `Njord:Enrichment:Indices`, etc.

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

### Requirement: Sub-option bindings coexist with parent EnrichmentOptions binding

The individual bindings SHALL NOT interfere with the existing
`IOptions<EnrichmentOptions>` binding. Both resolution paths — the parent
object and each sub-type — SHALL reflect the same config values.

#### Scenario: Dual resolution returns consistent values

- **WHEN** `Njord:Enrichment:Alerts:Enabled` is set to `true` in config
- **THEN** `IOptions<AlertOptions>.Value.Enabled` SHALL equal `true`
  AND `IOptions<EnrichmentOptions>.Value.Alerts.Enabled` SHALL equal `true`
