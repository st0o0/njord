## MODIFIED Requirements

### Requirement: Alert record serialization
`AlertResult` and `Alert` records SHALL have `[property: JsonProperty("...")]` on all positional parameters producing camelCase wire names. The `Alert` record's `Attributes` dictionary property SHALL retain its `IReadOnlyDictionary<string, object?>` type with a pinned wire name.

#### Scenario: AlertResult round-trips through JSON with pinned wire names
- **WHEN** an `AlertResult` with alerts is serialized to JSON and deserialized back
- **THEN** all properties (Location, Alerts including nested Alert fields: Type, Severity, Confidence, Attributes, TriggerValue, Threshold, PeakValue, HoursUntil, DurationHours) round-trip correctly with camelCase wire names
