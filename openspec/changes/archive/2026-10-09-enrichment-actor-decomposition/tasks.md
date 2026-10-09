## 1. ConsensusFlowFactory extraction

- [x] 1.1 Create `src/Njord.Enrichment/ConsensusFlowFactory.cs` with a static `Create(IActorRef sensorHub, TimeSpan timeout, ...)` method that returns the consensus `Flow<>`, moving the logic from `EnrichmentActor.BuildConsensusInlineFlow` (lines ~251-305).
- [x] 1.2 Update `EnrichmentActor` to call `ConsensusFlowFactory.Create(...)` instead of the inline flow builder.
- [x] 1.3 Add `ConsensusFlowFactorySpec` in `src/Njord.Enrichment.Tests/` testing the flow in isolation (sensor snapshot available, sensor timeout, empty snapshot).
- [x] 1.4 Confirm existing `EnrichmentActorSpec` tests pass unmodified.

## 2. CapabilityTracker extraction

- [x] 2.1 Create `src/Njord.Egress/CapabilityTracker.cs` owning the `knownCapabilities` dictionary, `IsSubsetOf`/`UnionWith` logic, and `CapabilityLearned` event generation.
- [x] 2.2 Update `ModelStateActor` (lines ~102-143) to delegate capability tracking to `_tracker.Track(observedParams)` and react to the returned result.
- [x] 2.3 Add `CapabilityTrackerSpec` in `src/Njord.Egress.Tests/` covering: first observation, subset observation (no event), superset observation (event emitted).
- [x] 2.4 Confirm existing `ModelStateActorSpec` tests pass unmodified.

## 3. HorizonProjection decomposition

- [x] 3.1 Split `HorizonProjection.BuildPerHorizon()` into `ToHourlyPayload(ForecastPoint, parameters)` and `ToDailyPayload(DailyForecastPoint, parameters)` helper functions.
- [x] 3.2 Extract horizon-filtering logic (effective days, max hours) into a separate method.
- [x] 3.3 Add/extend `HorizonProjectionSpec` in `src/Njord.Egress.Tests/` covering the extracted functions independently.
- [x] 3.4 Confirm existing egress/MQTT golden-master tests pass unmodified.

## Validation

- [x] 4.1 `dotnet build Njord.slnx` (run from `src/`)
- [x] 4.2 `dotnet run --project Njord.Enrichment.Tests/Njord.Enrichment.Tests.csproj --no-build`
- [x] 4.3 `dotnet run --project Njord.Egress.Tests/Njord.Egress.Tests.csproj --no-build`
- [x] 4.4 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build`
- [x] 4.5 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
