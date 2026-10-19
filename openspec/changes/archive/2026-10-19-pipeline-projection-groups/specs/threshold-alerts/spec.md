## MODIFIED Requirements

### Requirement: Alert severity and confidence model
Alert severity SHALL remain threshold-based (Info, Warning, Critical). Alert confidence SHALL be derived from Ensemble standard deviation instead of model agreement count. The confidence categories (High, Medium, Low) SHALL use configurable StdDev thresholds.

#### Scenario: Confidence replaces model agreement
- **WHEN** an alert is computed from ensemble data
- **THEN** the alert SHALL carry a Confidence level derived from StdDev, not a models_agreed count
