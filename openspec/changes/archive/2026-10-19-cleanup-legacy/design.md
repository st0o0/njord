## Context

After pipeline-projection-groups and grpc-mqtt-v2, the old analysis projects become dead code. This change removes them and consolidates.

## Goals / Non-Goals

**Goals:** Remove dead code, update architecture tests and documentation.
**Non-Goals:** Adding new features.

## Decisions

### D1: Remove definitively: Consensus, Derived, Forecast

These are fully replaced and have no remaining consumers.

### D2: Trends — remove unless migrated

If not migrated as a ProjectionGroup in pipeline-projection-groups, remove entirely. Decision at implementation time based on whether trends were ported.

### D3: Indices — remove unless migrated

Same as Trends. The BBQ/Laundry scores are unique value but may not justify the maintenance. Decision at implementation time.

### D4: CycleId and LocationId removed

CycleId is replaced by ModelRun on DomainForecast. LocationId is replaced by Location. Remove both from Njord.Model.

### D5: Architecture tests rewritten

Layer references, zone enforcement, naming conventions all updated for the new ~12-project structure (down from ~20).
