## REMOVED Requirements

### Requirement: Energy in enrichment responses and streams
**Reason**: The `energy` field is removed from `GetEnrichmentsResponse` and `EnrichmentEvent` in the weather service proto. Energy events are no longer emitted via `GetEnrichments` or `StreamEnrichments`.
**Migration**: None. Proto field numbers are reserved to prevent reuse.
