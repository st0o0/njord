## MODIFIED Requirements

### Requirement: Option-derived values are produced by one extension
`ParametersOptions.Resolve()` SHALL produce the `ResolvedParameterSet` and be the only call site of `ParameterRegistry.Resolve` for options. `PollingOptions.ToBudgetAllowance()` SHALL produce the daily and monthly request allowance (`BudgetAllowance`) used by the budget tracker, the budget gauges and the budget health check.

#### Scenario: Validator and container agree
- **WHEN** the options validator and the container resolve the same `ParametersOptions`
- **THEN** both use `Resolve()` and obtain the same set
