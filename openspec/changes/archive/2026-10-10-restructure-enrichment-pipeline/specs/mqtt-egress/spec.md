## REMOVED Requirements

### Requirement: Device-based discovery for a static entity grid
**Reason**: MQTT Discovery lifecycle (retained config payloads, HA birth
re-discovery, tombstoning) is removed. MQTT becomes state-only.
**Migration**: Users configure HA entities manually or Discovery is
re-introduced later as a clean consumer-level feature.

### Requirement: Discovery component metadata adapts to registry
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides derived topic helpers
**Reason**: Enrichment-specific topic helpers tied to `EnrichmentDeviceId` and
`EnrichmentTypeNames` are removed with the string-based type system.
**Migration**: The MQTT consumer defines its own topic structure for analysis
results based on typed result metadata.

### Requirement: DiscoveryPayloadBuilder builds a derived device
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides trend topic helpers
**Reason**: Removed with string-based type system.
**Migration**: None — discovery capability is dropped.

### Requirement: DiscoveryPayloadBuilder builds a trend device
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides index topic helpers
**Reason**: Removed with string-based type system.
**Migration**: None — discovery capability is dropped.

### Requirement: DiscoveryPayloadBuilder builds an index device
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides history topic helpers
**Reason**: Removed with string-based type system.
**Migration**: None — discovery capability is dropped.

### Requirement: DiscoveryPayloadBuilder builds a history device
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

## MODIFIED Requirements

### Requirement: Missing values surface as unavailable, never as stale data
Every component SHALL become unavailable when (a) the service availability
topic reads `offline`, or (b) its value is absent in the state JSON, or
(c) no state update arrived within twice the poll interval (`expire_after`).
With discovery removed, the MQTT consumer SHALL publish `unavailable` state
when a feature produces no result for a poll cycle.

#### Scenario: Unsupported parameter has no sensor
- **WHEN** `icon_d2` does not support `precipitation_probability`
- **THEN** no sensor component for `precipitation_probability` exists in the state payload for icon_d2

#### Scenario: Out-of-range horizon has no sensor
- **WHEN** `icon_d2` has MaxForecastHours=48
- **THEN** no sensor components for horizon h72 exist in the state payload for icon_d2

#### Scenario: Silent model expires
- **WHEN** a device receives no state update for two poll intervals
- **THEN** its components become unavailable without any explicit publish

#### Scenario: Feature produces no result
- **WHEN** a poll cycle completes but a feature produces no result (e.g. trends
  on the first cycle)
- **THEN** the MQTT consumer publishes an `unavailable` state for that feature's
  entities

#### Scenario: Stale state expiry
- **WHEN** no poll cycle completes within 2× the poll interval
- **THEN** Home Assistant marks the entities as unavailable via `expire_after`
