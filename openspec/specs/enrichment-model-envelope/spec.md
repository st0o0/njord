# enrichment-model-envelope Specification

## Purpose

Per-model computation of Index scores, then aggregation into min/max/confidence envelope fields alongside existing single-value outputs. Exposes forecast uncertainty to HA automations.
## Requirements
### Requirement: Envelope fields appear in state payloads alongside existing values
State JSON for indices SHALL include envelope fields at the same level as existing fields. Existing field names and values SHALL NOT change.

#### Scenario: Indices state payload
- **WHEN** index result is serialized to state JSON
- **THEN** the JSON contains both `"outdoor": 72` and `"outdoor_min": 65, "outdoor_max": 80, "outdoor_confidence": 0.8`

