## MODIFIED Requirements

### Requirement: ThunderstormAlert incorporates atmospheric stability indicators
The existing ThunderstormAlert SHALL additionally evaluate convective inhibition (CIN) and lifted index from the weather forecast to modulate severity. Low CIN (< -50 J/kg, weak cap) combined with negative lifted index (< -2, unstable) SHALL increase severity by one step. High CIN (> 0 J/kg, strong cap) SHALL decrease severity by one step (minimum Yellow, never suppresses entirely).

#### Scenario: Unstable atmosphere escalates severity
- **WHEN** CAPE, precipitation, and wind gusts meet thresholds AND CIN is below -50 J/kg AND lifted index is below -2
- **THEN** the thunderstorm severity SHALL be escalated by one level (Yellow to Orange, Orange to Red)

#### Scenario: Strong cap de-escalates severity
- **WHEN** CAPE, precipitation, and wind gusts meet thresholds AND CIN is above 0 J/kg
- **THEN** the thunderstorm severity SHALL be reduced by one level (Red to Orange, Orange to Yellow) but never below Yellow

#### Scenario: Missing CIN or lifted index
- **WHEN** convective_inhibition or lifted_index data is not available in the forecast
- **THEN** the ThunderstormAlert SHALL behave as before (no stability modification)

#### Scenario: Updated domain requirements
- **WHEN** the ThunderstormAlert feature is enabled
- **THEN** its requirements SHALL include ConvectiveInhibition and LiftedIndex as optional weather parameters (not required, so missing data does not prevent the alert)
