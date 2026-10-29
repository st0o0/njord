## MODIFIED Requirements

### Requirement: Validator for dead Model property is removed
The OpenMeteoOptionsValidator SHALL NOT contain a validation branch for the removed ForecastOptions.Model property.

#### Scenario: No migration hint for Model
- **WHEN** ForecastOptions is validated
- **THEN** no validation message references a `Model` property or migration hint
