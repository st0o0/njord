## MODIFIED Requirements

### Requirement: Budget calculation for Ensemble models
The `BudgetGuard` SHALL calculate Ensemble call weight as `ceil(CallWeight.Of(allVars, days)) * modelCount` where `modelCount` is the number of configured Ensemble models. With 3 default models, the Ensemble contribution to the daily budget SHALL reflect the third model.

#### Scenario: Three-model budget estimate
- **WHEN** the system starts with 3 default Ensemble models and 2 locations
- **THEN** `BudgetGuard` SHALL project Ensemble cost as 3x the single-model weight per location
- **AND** the projected total SHALL remain within 80% of the monthly budget (300k)

#### Scenario: EnsembleModelCount in DomainCallConfig
- **WHEN** `DomainCallConfig` is built from resolved options
- **THEN** `EnsembleModelCount` SHALL reflect the actual number of configured models (3 by default)
