## REMOVED Requirements

### Requirement: Enrichment pipeline concerns are separated into focused components
**Reason**: `EnrichmentActor` and `ConsensusFlowFactory` are replaced by
Host-owned pipeline composition. The separation into stateless/stateful/actor
enrichment sub-interfaces is eliminated; each feature is a standalone library.
**Migration**: Pipeline composition moves to the Host's stream graph builder.
Consensus computation remains in `Njord.Compute`. Feature logic moves to
per-feature libraries.
