## MODIFIED Requirements

### Requirement: Envelope fields appear in discovery as additional sensor components
Discovery payloads for indices devices SHALL register additional sensor components for each envelope field (`_min`, `_max`, `_confidence`). They SHALL share the same state topic as the base fields and use `value_template` to extract the specific JSON key.

#### Scenario: Indices device discovery includes envelope
- **WHEN** the indices presenter's `BuildDiscoveryPayload` is called for the indices device
- **THEN** the payload contains components for `outdoor`, `outdoor_min`, `outdoor_max`, `outdoor_confidence` (and likewise for all other score fields)
