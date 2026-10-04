## Purpose

Defines how partial enrichment events from the streaming pipeline are merged into the coordinator's enrichment state, preserving fields not included in the update.

## Requirements

### Requirement: Partial enrichment events are merged
When a streaming `EnrichmentEvent` contains only a subset of enrichment fields, the merge SHALL update only the delivered fields and preserve all others from the existing `EnrichmentData`. The merge SHALL also track `derived_updated_at` from the event's `updated_at` timestamp when a derived payload is present, analogous to how `consensus_updated_at` is tracked.

#### Scenario: Alert event preserves indices
- **WHEN** an enrichment event with only `alerts` arrives for a location that already has `indices` data (including forecast list, frost, and vpd)
- **THEN** the resulting `EnrichmentData` has the new `alerts` and the previous `indices` unchanged (forecast, frost, vpd all preserved)

#### Scenario: Index event preserves alerts
- **WHEN** an enrichment event with only `indices` arrives for a location that already has `alerts`
- **THEN** the resulting `EnrichmentData` has the new `indices` (with new IndexData shape including forecast/frost/vpd) and the previous `alerts` unchanged

#### Scenario: All enrichment types can be merged independently
- **WHEN** enrichment events arrive for alerts, indices, trends, derived, history, or consensus individually
- **THEN** each is merged independently without affecting the other fields

#### Scenario: IndexData default detection works with new shape
- **WHEN** an enrichment event arrives with `indices` set to `IndexData()` (all defaults: scores=0, frost=None, vpd=None, forecast=[])
- **THEN** the merge treats this as a non-default value (indices field is present) and updates the existing indices

#### Scenario: Derived event sets derived_updated_at
- **WHEN** an enrichment event with a derived payload arrives with `updated_at = 2025-01-15T12:00:00Z`
- **THEN** the resulting `EnrichmentData` has `derived_updated_at = 2025-01-15T12:00:00Z`

#### Scenario: Non-derived event preserves derived_updated_at
- **WHEN** an enrichment event with only alerts arrives for a location that has `derived_updated_at` set
- **THEN** the `derived_updated_at` is preserved unchanged

### Requirement: Merge uses immutable replacement
The merge SHALL produce a new `EnrichmentData` via `dataclasses.replace()`, not by mutating the existing object.

#### Scenario: Original object is unchanged
- **WHEN** a merge produces an updated `EnrichmentData`
- **THEN** the original `EnrichmentData` object is not modified (frozen dataclass guarantee)

### Requirement: Merge handles missing base data
When an enrichment event arrives for a location with no prior enrichment data, the merge SHALL create a new `EnrichmentData` with the event's payload as the only populated field.

#### Scenario: First enrichment for a location
- **WHEN** an alert event arrives for location "zurich" and no enrichment data exists for "zurich"
- **THEN** a new `EnrichmentData(location="zurich", alerts=[...])` is created with all other fields at defaults
