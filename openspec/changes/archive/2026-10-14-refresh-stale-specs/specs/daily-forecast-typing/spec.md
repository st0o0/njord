## REMOVED Requirements

### Requirement: Actor messages do not reside in the Domain layer
**Reason**: The history messages it places (`RecordSnapshot`, `QueryHistory`, `HistoryResponse`, `ForecastRecord`), the `Njord.Enrichment` and `Njord.Domain` namespaces and `ForecastHistoryActor` no longer exist. Message placement is now governed by `message-conventions` (all public messages in `Njord.Messages`) and the zone rules.
**Migration**: See `message-conventions` and `architecture-zone-enforcement`.
