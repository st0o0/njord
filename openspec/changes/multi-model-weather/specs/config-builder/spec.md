## ADDED Requirements

### Requirement: Model selection in the builder
The Config Builder SHALL list the supported weather models with provider, resolution and forecast length, let the user enable them and choose the primary, and emit `Njord:Weather:Models` and the `ModelComparison` feature accordingly.

#### Scenario: Add a model
- **WHEN** the user enables a second model
- **THEN** the exported configuration lists both ids in order and enables `ModelComparison`

#### Scenario: Per-model budget impact
- **WHEN** a model is toggled
- **THEN** the builder shows the added calls per month from the same formula as `BudgetCalculator` and updates the budget meter
