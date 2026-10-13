## MODIFIED Requirements

### Requirement: All analyzers receive their options via IOptions of T

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
