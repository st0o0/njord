## ADDED Requirements

### Requirement: All analyzers receive their options via IOptions of T

Each analyzer (`AlertAnalyzer`, `IndexAnalyzer`, `TrendAnalyzer`,
`DerivedAnalyzer`) SHALL accept its feature-specific options via
`IOptions<T>` constructor injection. No analyzer SHALL accept raw option
types, extracted primitives, or constructor-default booleans that bypass
config.

#### Scenario: IndexAnalyzer receives IOptions of IndexOptions

- **WHEN** `IndexAnalyzer` is resolved from DI
- **THEN** its constructor SHALL have received `IOptions<IndexOptions>`,
  not a raw `IndexOptions` instance

#### Scenario: TrendAnalyzer receives IOptions of TrendOptions

- **WHEN** `TrendAnalyzer` is resolved from DI
- **THEN** its constructor SHALL have received `IOptions<TrendOptions>`,
  not a `bool enabled` parameter

#### Scenario: DerivedAnalyzer receives IOptions of NjordOptions for horizons

- **WHEN** `DerivedAnalyzer` is resolved from DI
- **THEN** its constructor SHALL have received `IOptions<NjordOptions>`
  (for horizons) via DI, not via a manual factory extracting primitives

### Requirement: Analyzer enabled state reflects config

Each analyzer's enabled/disabled state SHALL be determined by its options
(`XxxOptions.Enabled`), resolved from config at startup. A constructor
SHALL NOT supply a default that silently overrides config.

#### Scenario: TrendAnalyzer disabled when config says disabled

- **WHEN** `Njord:Enrichment:Trends:Enabled` is `false`
- **THEN** `TrendAnalyzer` SHALL report itself as disabled

#### Scenario: IndexAnalyzer enabled when config says enabled

- **WHEN** `Njord:Enrichment:Indices:Enabled` is `true`
- **THEN** `IndexAnalyzer` SHALL report itself as enabled

### Requirement: No manual DI factories for analyzers

Analyzer registration SHALL use standard `AddSingleton<T>()` auto-resolution
or a minimal factory that only resolves `IOptions<T>` types. No factory
SHALL extract individual properties from options and pass them as constructor
primitives.

#### Scenario: DerivedAnalyzer registered without manual horizon extraction

- **WHEN** the analysis setup container registers `DerivedAnalyzer`
- **THEN** the registration SHALL NOT contain a factory that reads
  `IOptions<NjordOptions>.Value.Horizons` and passes it as a list
