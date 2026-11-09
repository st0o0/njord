## Context

`EntityIdLookup` maps `(FeatureResult CLR type, location)` to entity IDs. Other OutputKinds have per-feature result subtypes (e.g. `FrostAlertResult`, `HeatAlertResult`), so the CLR type uniquely identifies the feature. Observation features all emit the same `ObservationResult`, so the lookup finds no registered type and silently drops them from v3 entity updates. ManifestBuilder correctly declares the entities, but they never receive state.

## Goals / Non-Goals

**Goals:**
- Observation features produce v3 entity state updates routed to the correct entity ID
- Minimal change surface: only Observation-specific code paths change

**Non-Goals:**
- Per-feature Observation subtypes (boilerplate for identical `(Location, Value)` records)
- Changing `FeatureResult` base class
- Proto changes

## Decisions

### Decision 1: Add `FeatureName` to `ObservationResult`

Change `ObservationResult(Location, double Value)` to `ObservationResult(Location, string FeatureName, double Value)`.

**Alternative considered:** Per-feature subtype hierarchy (`Pm25ObservationResult : ObservationFeatureResult`). Rejected because all observation results carry the same data (`double Value`) — the subtypes would be empty boilerplate unlike Alert/Score/Condition subtypes which carry feature-specific fields.

**Alternative considered:** Add `FeatureName` to `FeatureResult` base class. Rejected because only Observation needs it; all other OutputKinds distinguish by CLR type. Adding it to the base forces 60+ existing features to pass a name they don't need.

### Decision 2: Separate observation lookup in `EntityIdLookup`

Add a second dictionary `Dictionary<(string FeatureName, string Location), string>` alongside the existing `Dictionary<(Type, string Location), string>`.

In the constructor, for `OutputKind.Observation` descriptors: register in the observation dictionary keyed by `(descriptor.Name, location)` instead of trying `FindResultType`.

In `Find`: pattern-match `ObservationResult` first and look up by `(result.FeatureName, result.Location.Name)`. Fall through to the existing type-based lookup for all other result kinds.

**Rationale:** Keeps the existing type-based path untouched. Weather already has a special case (line 27-30 in current code); Observation gets the same treatment with a name-based key instead of a hardcoded type.

### Decision 3: Each Observation FeatureRule passes `Descriptor.Name`

In `Evaluate`, change `new ObservationResult(ctx.Location, value)` to `new ObservationResult(ctx.Location, Descriptor.Name, value)`.

## Risks / Trade-offs

- **[Breaking record change]** `ObservationResult` positional parameter order changes. Pre-1.0, no persistence impact (Observation results are not persisted). v3 proto is unchanged (`ObservationValue` is just `double value`).
- **[Name coupling]** The lookup keys on the string from `Descriptor.Name`. If a feature is renamed, the entity ID changes. This is the same coupling `ManifestBuilder` already has — both derive from the same descriptor name.

## Open Questions

None.
