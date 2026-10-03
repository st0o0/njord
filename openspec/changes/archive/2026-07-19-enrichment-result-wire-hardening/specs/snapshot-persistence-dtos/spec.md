## ADDED Requirements

### Requirement: Enrichment result inner JSON wire names are pinned
All enrichment result records serialized inside `EnrichmentEntryDto.JsonPayload` SHALL have `[JsonProperty]` attributes on every property, producing stable camelCase wire names. Value tuples SHALL be replaced with named records carrying `[JsonProperty]` attributes.

#### Scenario: Enrichment result round-trips through nested JSON with stable wire names
- **WHEN** an enrichment result (e.g., `IndexResult`) is serialized via `EnrichmentSnapshotMapping.ToDto` and deserialized via `EnrichmentSnapshotMapping.ToDomain`
- **THEN** all property values round-trip correctly and the JSON wire format matches the Verify-approved snapshot

#### Scenario: Unknown fields in nested JSON are ignored on deserialization
- **WHEN** a persisted `EnrichmentEntryDto.JsonPayload` contains JSON fields not present in the current record definition
- **THEN** deserialization succeeds and the unknown fields are silently ignored

### Requirement: CLAUDE.md caveat is removed
The CLAUDE.md caveat about `EnrichmentEntryDto` inner-JSON limitation SHALL be removed once all enrichment result records are hardened.

#### Scenario: CLAUDE.md no longer warns about inner-JSON gap
- **WHEN** all enrichment result records have `[JsonProperty]` on every property
- **THEN** the caveat sentence in CLAUDE.md Conventions is removed
