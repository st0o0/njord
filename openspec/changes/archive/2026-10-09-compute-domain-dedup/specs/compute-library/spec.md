## ADDED Requirements

### Requirement: Repeated horizon-scan and threshold-classification shapes are shared
Alert computations in `AlertEvaluator` that scan a parameter's `ByHorizon` consensus data
up to a maximum-hours cutoff to find an extreme median value, the earliest qualifying
hour, and an agreement list SHALL use one shared helper for that scan instead of a
per-alert-type copy of the loop. Alert computations that classify an extreme value against
a 3-tier threshold list into an `AlertSeverity` SHALL use one shared classification helper
instead of a per-alert-type copy of the switch expression.

#### Scenario: New alert type reuses the shared scan and classification helpers
- **WHEN** a new alert type is added to `AlertEvaluator`
- **THEN** its implementation calls the shared horizon-scan helper and the shared
  threshold-classification helper, and does not introduce a new copy of either loop/switch
  shape

#### Scenario: Existing alert results are unchanged
- **WHEN** any existing alert type's evaluation method is migrated to use the shared
  helpers
- **THEN** its computed severity, confidence, and earliest-qualifying-hour for every
  existing `Njord.Compute.Tests` case remain identical to the pre-migration result
