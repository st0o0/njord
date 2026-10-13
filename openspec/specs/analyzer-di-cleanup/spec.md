# analyzer-di-cleanup Specification

## Purpose

Ensures all analysis feature analyzers use `IOptions<T>` constructor injection
consistently, with no raw types, extracted primitives, or constructor-default
booleans that bypass config.
## Requirements
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

### Requirement: Analyzers read only their toggle from options and take settings per computation

Each analyzer (`AlertAnalyzer`, `IndexAnalyzer`, `TrendAnalyzer`, `DerivedAnalyzer`) SHALL read only its `Enabled` toggle from `IOptions<T>` at construction. All other parameters SHALL be passed per computation as the current immutable settings record, so a hot configuration change takes effect on the next poll cycle. No analyzer SHALL inject `IOptions<NjordOptions>`, `IOptionsMonitor<T>`, raw option types, extracted primitives, or constructor-default booleans that bypass config.

#### Scenario: IndexAnalyzer receives current settings per computation

- **WHEN** index preferences are changed at runtime and the next poll cycle runs
- **THEN** `IndexAnalyzer` computes with the new resolved preferences without being reconstructed

#### Scenario: TrendAnalyzer receives IOptions of TrendOptions

- **WHEN** `TrendAnalyzer` is resolved from DI
- **THEN** its constructor SHALL have received `IOptions<TrendOptions>`,
  not a `bool enabled` parameter

#### Scenario: DerivedAnalyzer receives horizons through settings

- **WHEN** `DerivedAnalyzer` computes a result
- **THEN** it reads the configured horizons from the settings passed to the computation,
  not from an injected `IOptions<NjordOptions>`

