## MODIFIED Requirements

### Requirement: Pipeline evaluates only enabled handlers
The pipeline SHALL filter IDomainHandler instances by their ConfigKey against EnrichmentOptions at materialization time. Disabled handlers SHALL not be evaluated even if their domain data is present.

#### Scenario: Handler disabled despite domain available
- **WHEN** Weather data arrives and LaundryScore is disabled
- **THEN** LaundryScoreHandler SHALL not be called even though Weather data is present
