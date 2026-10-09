## 1. Confirm existing snapshot coverage

- [x] 1.1 Confirm each of `AlertPresenter`, `DerivedPresenter`, `IndexPresenter`, `TrendPresenter`, `HistoryPresenter` has a Verify-snapshot-backed discovery-payload test in `src/Njord.Mqtt.Tests/` (per the `mqtt-enrichment-presentation` "Discovery payload snapshots" requirement). Add any missing snapshot test before proceeding.

## 2. Shared component spec/builder

- [x] 2.1 Add `SensorComponentSpec` record to `src/Njord.Mqtt/DiscoveryPayloadBuilder.cs` (or a new `src/Njord.Mqtt/SensorComponentSpec.cs`), covering platform, unit, device class, state topic, value template, and a list of (topic, optional value template) availability entries.
- [x] 2.2 Add `DiscoveryPayloadBuilder.ToComponent(SensorComponentSpec spec, string availabilityTopic, int expireAfterSeconds) -> JsonObject`, producing the same JSON shape currently hand-written in the presenters (including `availability_mode = "all"`).
- [x] 2.3 Add `DiscoveryPayloadBuilderSpec` cases for `ToComponent`: sensor platform with unit+device class, binary_sensor platform, single vs. multi-entry availability.

## 3. Migrate model grid + consensus to the shared builder

- [x] 3.1 Rewrite `DiscoveryPayloadBuilder.Build` (model grid) to construct `SensorComponentSpec` values from `ParameterDef` and call `ToComponent`, replacing `BuildComponent`.
- [x] 3.2 Rewrite `DiscoveryPayloadBuilder.BuildConsensus` the same way.
- [x] 3.3 Remove the now-unused `BuildComponent` method.
- [x] 3.4 Run `Njord.Mqtt.Tests` and confirm `MqttDiscoveryActorSpec`/`ConsensusPresenter` Verify snapshots are unchanged.

## 4. Migrate the five presenters

- [x] 4.1 `AlertPresenter`: replace the inline `JsonObject` per `AlertType` with a `SensorComponentSpec` list + `ToComponent`. Verify snapshot unchanged.
- [x] 4.2 `DerivedPresenter`: replace inline literals (horizon params + meta sensors) with specs + `ToComponent`. Verify snapshot unchanged.
- [x] 4.3 `IndexPresenter`: replace the existing `scoreSensors`/`numericSensors` tuple arrays' hand-rolled loop bodies with specs + `ToComponent`. Verify snapshot unchanged.
- [x] 4.4 `TrendPresenter`: replace `textSensors`/`numericSensors` loop bodies with specs + `ToComponent`. Verify snapshot unchanged.
- [x] 4.5 `HistoryPresenter`: replace the per-model MAE/weight/drift loop and the four fixed sensors (`seasonal_best`, `anomaly`, `anomaly_deviation`, `weighted_temperature`) with specs + `ToComponent`. Verify snapshot unchanged.

## 5. Remove dead code

- [x] 5.1 Delete `DiscoveryPayloadBuilder.BuildAlerts`, `BuildHistory`, `BuildIndices`, `BuildTrends`, `BuildDerived` (confirmed zero production callers — all 5 methods only called from their test file).
- [x] 5.2 Delete the corresponding test cases in `src/Njord.Mqtt.Tests/DiscoveryPayloadBuilderSpec.cs` that exercise only those four methods.
- [x] 5.3 Grep the whole `src/` tree for `BuildAlerts`, `BuildHistory`, `BuildIndices`, `BuildTrends` to confirm no remaining references.

## Validation

- [x] 6.1 `dotnet build Njord.slnx` (run from `src/`)
- [x] 6.2 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build`
- [x] 6.3 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
