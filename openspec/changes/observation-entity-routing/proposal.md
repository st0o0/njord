## Why

All Observation features emit the same `ObservationResult` type. `EntityIdLookup.FindResultType` tries name-based suffixes (`Pm25ObservationResult` etc.) but no such types exist, so observations are never registered. `GrpcConsumer` skips v3 entity events for all 4 Observation features. ManifestBuilder declares the entities but they never receive state — v3 subscribers see phantom entities with no updates.

## What Changes

- Add `string FeatureName` positional parameter to `ObservationResult` so the lookup can distinguish which feature produced the result
- Change `EntityIdLookup` to handle `OutputKind.Observation` with a separate `(typeof(ObservationResult), featureName, location)` path, similar to how it already special-cases `OutputKind.Weather`
- Update `EntityIdLookup.Find` to extract the feature name from `ObservationResult`
- Update all 4 Observation FeatureRules to pass `Descriptor.Name` when constructing results
- Update all affected tests

No budget impact — this change does not add or alter polling.

## Non-goals

- Creating per-feature Observation subtypes (all share `(Location, Value)`, a type hierarchy adds boilerplate with no semantic value)
- Changing `FeatureResult` base class (only `ObservationResult` needs identification by name)
- Changing how other OutputKinds work (they have per-feature types that the lookup already resolves)
- Proto changes (the v3 `ObservationValue` message is unchanged)

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `observation-features`: ObservationResult gains a `FeatureName` field; observation features pass their descriptor name
- `unified-entity-stream`: EntityIdLookup correctly routes Observation results to v3 entity updates (fixes the broken Observation scenario in the spec)

## Impact

- `Njord.Messages`: `ObservationResult` record signature changes (**BREAKING** persistence — pre-1.0, acceptable)
- `Njord.Features`: 4 Observation rules updated
- `Njord.Grpc`: `EntityIdLookup` constructor and `Find` method
- Tests: `Njord.Messages.Tests`, `Njord.Features.Tests`, `Njord.Grpc.Tests`
