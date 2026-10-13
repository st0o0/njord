## ADDED Requirements

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

## REMOVED Requirements

### Requirement: All analyzers receive their options via IOptions of T
**Reason**: Constructor-injected IOptions values freeze at startup, which blocks hot reload of analysis parameters.
**Migration**: Analyzers read Enabled from IOptions<T> and receive the current settings record per computation (see the added requirement).
