## REMOVED Requirements

### Requirement: Energy config in SetEnrichment
**Reason**: The `EnergyConfig` message and `energy` field in `SetEnrichmentRequest` and `DetailedEnrichmentConfig` are removed. The `SetEnrichment` RPC no longer accepts or returns energy configuration.
**Migration**: None. Proto field numbers are reserved to prevent reuse.
