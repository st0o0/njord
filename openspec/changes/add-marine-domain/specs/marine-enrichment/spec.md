## ADDED Requirements

### Requirement: Swimming conditions rates ocean safety
The SwimmingConditionsHandler SHALL evaluate wave height and water temperature to produce a safety rating.

#### Scenario: Calm warm waters
- **WHEN** wave height = 0.5m and water temperature = 22°C
- **THEN** SwimmingConditions SHALL be "Safe"

#### Scenario: High waves are dangerous
- **WHEN** wave height = 2.5m
- **THEN** SwimmingConditions SHALL be "Dangerous"

#### Scenario: Cold water warning
- **WHEN** wave height = 0.3m but water temperature = 12°C
- **THEN** SwimmingConditions SHALL include a cold water warning
