## REMOVED Requirements

### Requirement: Device-based discovery for static entity grid
**Reason**: MQTT Discovery lifecycle (retained config payloads, HA birth
re-discovery, tombstoning) is removed. MQTT becomes state-only.
**Migration**: Users configure HA entities manually or Discovery is
re-introduced later as a clean consumer-level feature.

### Requirement: DiscoveryPayloadBuilder builds derived/trend/index/history devices
**Reason**: Discovery is removed entirely.
**Migration**: None — discovery capability is dropped.

### Requirement: TopicScheme provides derived/trend/index/history topic helpers
**Reason**: Enrichment-specific topic helpers tied to `EnrichmentDeviceId` and
`EnrichmentTypeNames` are removed with the string-based type system.
**Migration**: The MQTT consumer defines its own topic structure for analysis
results based on typed result metadata.

## MODIFIED Requirements

### Requirement: Missing values surface as unavailable
The MQTT consumer SHALL publish `unavailable` state when a feature produces
no result for a poll cycle. State messages SHALL carry `expire_after` set to
2× the poll interval so stale values are automatically marked unavailable by
Home Assistant.

#### Scenario: Feature produces no result
- **WHEN** a poll cycle completes but a feature produces no result (e.g. trends
  on the first cycle)
- **THEN** the MQTT consumer publishes an `unavailable` state for that feature's
  entities

#### Scenario: Stale state expiry
- **WHEN** no poll cycle completes within 2× the poll interval
- **THEN** Home Assistant marks the entities as unavailable via `expire_after`
