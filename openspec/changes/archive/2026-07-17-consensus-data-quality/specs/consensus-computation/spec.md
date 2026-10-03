## ADDED Requirements

### Requirement: Consensus MQTT payload carries per-parameter model counts
The consensus MQTT state payload SHALL include a `{parameter_key}_models` integer field for each parameter, indicating how many models contributed a non-null value for that parameter at that horizon. The global `_models_used` field SHALL be removed.

#### Scenario: Parameter with full model coverage
- **WHEN** 4 models contribute `temperature_2m` values at h3
- **THEN** the MQTT payload for h3 SHALL contain `"temperature_2m_models": 4`

#### Scenario: Parameter with partial model coverage
- **WHEN** only 1 model contributes `precipitation_probability` at h3 (other models don't have this parameter)
- **THEN** the MQTT payload for h3 SHALL contain `"precipitation_probability": null` (filtered by ≥2 rule) and `"precipitation_probability_models": 0`

#### Scenario: Global _models_used removed
- **WHEN** a consensus MQTT payload is published
- **THEN** the payload SHALL NOT contain a `_models_used` field
