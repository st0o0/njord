## ADDED Requirements

### Requirement: Trend math class name does not collide with the analyzer
The static class holding pure trend math SHALL be named `TrendFormulas` (following the `<Feature>Formulas` scheme) and live next to the DI-registered `TrendAnalyzer` in `Njord.Trends`, so no two public types share the name `TrendAnalyzer`.

#### Scenario: Pipeline references the analyzer without ambiguity
- **GIVEN** `Njord.Pipeline` references `Njord.Compute` and `Njord.Trends`
- **WHEN** it references `TrendAnalyzer`
- **THEN** exactly one public type named `TrendAnalyzer` exists, `Njord.Trends.TrendAnalyzer`

## REMOVED Requirements

### Requirement: Compute-layer trend functions have unique name
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Trend math class name does not collide with the analyzer" in this capability.
