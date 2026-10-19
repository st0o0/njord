## ADDED Requirements

### Requirement: Flood risk alert from discharge vs percentiles
The FloodRiskAlert rule SHALL compare forecast river discharge against historical percentiles and raise alerts based on severity.

#### Scenario: Discharge above p75 triggers warning
- **WHEN** river discharge exceeds the p75 percentile
- **THEN** an AlertRaised event SHALL be emitted with Warning severity

#### Scenario: Discharge above historical max triggers critical
- **WHEN** river discharge exceeds the historical maximum
- **THEN** an AlertRaised event SHALL be emitted with Critical severity

### Requirement: Cross-domain flood-rain correlation (optional)
The FloodRainAlert rule SHALL correlate heavy precipitation from Weather data with rising discharge from Flood data to produce a combined risk assessment.

#### Scenario: Heavy rain and rising discharge triggers elevated alert
- **WHEN** Weather shows heavy precipitation AND Flood shows rising discharge above median
- **THEN** an AlertRaised event SHALL be emitted noting the rain-flood correlation
