## ADDED Requirements

### Requirement: No string-based parameter lookups in analysis code
All analysis code — `ConsensusBuilder`, the alert rules, the index scores and `DaySlicer`, `DerivedAnalyzer` and `TrendAnalyzer` — SHALL resolve parameters via `ParameterRegistry` static properties (and `ResolvedParameterSet.Get(ParameterDef)` where the active set matters). No analysis code SHALL contain `FirstOrDefault(p => p.ApiName == ...)` or string-literal parameter names.

#### Scenario: Index slicing uses typed access
- **WHEN** `DaySlicer` resolves the `is_day` parameter
- **THEN** it uses `ParameterRegistry.IsDay`, not a string literal

#### Scenario: Alert rules use typed access
- **WHEN** `FrostRule` resolves the temperature parameter
- **THEN** it uses `ParameterRegistry.Temperature2m`, not `ParameterRegistry.GetByApiName("temperature_2m")`

### Requirement: Trend analysis uses ParameterDef keys, not strings
`TrendAnalyzer` SHALL define its tracked parameters as a `ParameterDef[]` and its per-parameter trend thresholds (`TrendThresholds`) as a dictionary keyed by `ParameterDef`, both using `ParameterRegistry` static properties. It SHALL NOT use `string[]` or `Dictionary<string, double>` for parameter identification.

#### Scenario: TrendParams uses typed array
- **WHEN** `TrendAnalyzer` defines its tracked parameters
- **THEN** it uses a `ParameterDef[]` referencing `ParameterRegistry` properties

## MODIFIED Requirements

### Requirement: Mean24h is not duplicated
There SHALL be no private 24-hour mean helper in the analysis libraries. Index scoring SHALL use the period means computed once by `DaySlicer`, and series means SHALL use `ForecastSeries.Mean` or the consensus query vocabulary.

#### Scenario: No private Mean24h methods
- **WHEN** the index library (`src/Njord.Indices`) is inspected
- **THEN** it contains no private `Mean24h` method

## REMOVED Requirements

### Requirement: No string-based parameter lookups in domain Compute methods
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "No string-based parameter lookups in analysis code" in this capability.

### Requirement: TrendResult uses ParameterDef keys, not strings
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Trend analysis uses ParameterDef keys, not strings" in this capability.

### Requirement: HistoryAnalyzer uses ParameterDef, not strings
**Reason**: `HistoryAnalyzer` and `ForecastHistory` no longer exist.
**Migration**: None.
