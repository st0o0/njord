## MODIFIED Requirements

### Requirement: README has feature highlights
The README SHALL include a concise feature section (4-6 bullet points)
summarizing njord's key capabilities as they exist: multi-model forecasts, multiple
locations, the analysis features (consensus, alerts, derived values, trends, indices),
gRPC for Home Assistant with optional MQTT state output, external sensor input, and low resource usage.
It SHALL NOT advertise removed features such as MQTT auto-discovery or forecast accuracy tracking.

#### Scenario: Features section is scannable
- **WHEN** a user reads the README
- **THEN** they can understand njord's value proposition within 10 seconds
  from the feature bullets
