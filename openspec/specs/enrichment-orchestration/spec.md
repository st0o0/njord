# enrichment-orchestration Specification

## Purpose
TBD - created by archiving change enrichment-actor-decomposition. Update Purpose after archive.
## Requirements
### Requirement: Enrichment pipeline concerns are separated into focused components
The consensus-flow construction, capability tracking, and graph topology assembly SHALL
each live in their own named component rather than being mixed in a single actor.

#### Scenario: Consensus flow is independently testable
- **WHEN** testing the consensus inline flow logic (sensor polling, timeout handling, metrics)
- **THEN** the test calls a `ConsensusFlowFactory.Create(...)` method directly without
  constructing an `EnrichmentActor` or its full dependency graph

#### Scenario: Capability tracking is side-effect-free in the stream
- **WHEN** `ModelStateActor`'s stream transformation processes a forecast update
- **THEN** the transformation itself is a pure data-mapping step that delegates capability
  tracking to a separate `CapabilityTracker`, and the stream stage contains no mutable
  dictionary mutations

