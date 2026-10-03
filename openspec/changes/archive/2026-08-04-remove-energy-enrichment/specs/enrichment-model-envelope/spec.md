## REMOVED Requirements

### Requirement: Energy envelope fields
**Reason**: The envelope pattern (pessimistic/optimistic bounds computed from consensus confidence intervals) was used exclusively by the energy enrichment for `HeatingDemandMax`, `CopEstimateMin`, and `CopOptimalConservative`. With energy removed, these fields and the envelope computation in `EnergyResult.ComputeEnvelope` are deleted.
**Migration**: None. No other enrichment currently uses the envelope pattern. If a future enrichment needs envelope bounds, the pattern can be reintroduced at that time.
