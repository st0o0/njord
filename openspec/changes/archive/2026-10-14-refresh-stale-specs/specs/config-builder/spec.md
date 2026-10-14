## MODIFIED Requirements

### Requirement: Live budget calculator validates configuration
The builder SHALL compute and display projected monthly API usage using the same formula as `BudgetCalculator` (applied at startup by `PollingOptionsValidator`): `totalModelsPerCycle × cyclesPerMonth × apiCallWeight`. It SHALL show the projection against the budget (default 300k or override), display a percentage bar, and warn when exceeding the 80% guard.

#### Scenario: Budget bar updates on model change
- **WHEN** a user adds a new model to all locations
- **THEN** the budget bar updates immediately to reflect the increased projected usage

#### Scenario: Over-budget warning
- **WHEN** projected usage exceeds 80% of the budget
- **THEN** a warning is displayed with suggestions to reduce usage

### Requirement: Enrichment toggles with per-feature settings
The builder SHALL show toggles for each enrichment feature (consensus, alerts, derived, trends, indices). When expanded, each feature SHALL show its configurable settings with defaults pre-filled.

#### Scenario: Enrichment feature toggles
- **WHEN** a user opens the enrichment section and expands a feature
- **THEN** toggles exist for consensus, alerts, derived, trends and indices (no history toggle), and the expanded feature shows its configurable settings pre-filled with defaults
