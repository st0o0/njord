## 1. ModelSnapshot Domain Type

- [x] 1.1 `ModelSnapshot` immutable record in `src/Njord/Domain/ModelSnapshot.cs`: `IReadOnlyDictionary<(string Location, WeatherModel Model), ModelForecast> Entries`, `static ModelSnapshot Empty`, `Update(ModelForecast)` returning new snapshot, `HasChanged` flag, `ModelsFor(string location)`. Change detection via `CycleId` comparison (not reference equality).
- [x] 1.2 Unit tests in `src/Njord.Tests/Domain/ModelSnapshotSpec.cs`: empty snapshot, first update adds entry, update replaces existing, different models coexist, HasChanged on new/changed/identical, ModelsFor with multiple locations, ModelsFor unknown location.

## 2. EnrichmentOptions Configuration

- [x] 2.1 `EnrichmentOptions` in `src/Njord/Configuration/EnrichmentOptions.cs`: `ConsensusOptions Consensus` with `bool Enabled` (default `true`), `string Method` (default `"Median"`), `double TrimPercent` (default `0.1`). Prepare for future consumers with extensible structure.
- [x] 2.2 Add `EnrichmentOptions Enrichment` property to `NjordOptions` in `src/Njord/Configuration/NjordOptions.cs`. Bind in `Program.cs` via the existing options pattern.

## 3. MqttEgressActor SinkRef Protocol

- [x] 3.1 `RequestMqttSink` / `MqttSinkResponse(ISinkRef<MqttMessage>)` message records in `src/Njord/Egress/EgressMessages.cs`.
- [x] 3.2 `RequestMqttSink` handler in `MqttEgressActor` (`src/Njord/Egress/MqttEgressActor.cs`): materializes a `SinkRef<MqttMessage>` connected to the existing `_mergeHubSink` and responds with `MqttSinkResponse`. Stash if received before graph materialization.
- [x] 3.3 Unit test in `src/Njord.Tests/Egress/MqttEgressActorSpec.cs`: verify SinkRef is vended after graph materialization and messages flow through the MergeHub.

## 4. ConsensusComputer — Pure Functions

- [x] 4.1 `ConsensusComputer.ComputeMedian(IReadOnlyList<double?> values)` in `src/Njord/Enrichment/ConsensusComputer.cs`. Returns `double?`. Unit tests in `src/Njord.Tests/Enrichment/ConsensusComputerSpec.cs`: odd count, even count, all null, single value.
- [x] 4.2 `ConsensusComputer.ComputeTrimmedMean(IReadOnlyList<double?> values, double trimPercent)`. Sorts, trims floor(n × trimPercent) from each end, averages remainder. Falls back to simple mean for <3 values. Tests: 10% on 8, 20% on 10, <3 values.
- [x] 4.3 `ConsensusComputer.ComputeSpread(IReadOnlyList<double?> values)`. Max − min of non-null. Null if <2. Tests: normal spread, single value.
- [x] 4.4 `ConsensusComputer.ComputeIqr(IReadOnlyList<double?> values)`. P75 − P25 via linear interpolation. Null if <4. Tests: 8 values, <4 values.
- [x] 4.5 `ConsensusComputer.ComputeAgreement(IReadOnlyList<double?> values, double reference, double tolerance)`. Fraction within ±tolerance. Null if no non-null values. Tests: full agreement, partial, empty.
- [x] 4.6 `ConsensusComputer.IdentifyOutlier(IReadOnlyList<(WeatherModel Model, double? Value)> models, double reference)`. Model with largest absolute deviation. Null if no values. Tests: clear outlier, all equal.
- [x] 4.7 `ConsensusComputer.ComputeConfidenceInterval(IReadOnlyList<double?> values, double lowerPct, double upperPct)`. Percentiles via linear interpolation. Null if <2. Tests: P10/P90 on 8 values, single value.
- [x] 4.8 `ConsensusComputer.BuildAvailabilityMatrix(ModelSnapshot snapshot, DateTimeOffset targetTime, string location)`. Per-model bool for data presence at horizon. Tests: model with data, model beyond horizon.

## 5. ConsensusResult and Serialization

- [x] 5.1 `ConsensusResult` record in `src/Njord/Enrichment/ConsensusResult.cs`: per (parameter, horizon) holding median, trimmedMean, spread, iqr, agreement, outlier, confidence interval, available models. `Compute(ModelSnapshot, ResolvedParameterSet, IReadOnlyList<int> horizons, TimeProvider)` factory that orchestrates all ConsensusComputer functions.
- [x] 5.2 `ConsensusResult.ToMqttMessages(string baseTopic, string location)`: produces one `MqttMessage` per horizon with topic `{baseTopic}/{location}/consensus/{horizon}` and flat JSON payload including parameter values + diagnostic attributes (spread, agreement, models_used). Tests in `src/Njord.Tests/Enrichment/ConsensusResultSpec.cs`.

## 6. Topic Scheme Extension

- [x] 6.1 `TopicScheme.ConsensusDeviceId(string location)` returning `njord_{slug}_consensus` in `src/Njord/Egress/TopicScheme.cs`.
- [x] 6.2 `TopicScheme.ConsensusHorizonTopic(string baseTopic, string location, string horizon)` returning `{baseTopic}/{slug}/consensus/{horizon}`.
- [x] 6.3 Tests in `src/Njord.Tests/Egress/TopicSchemeSpec.cs`: consensus device id, consensus horizon topic.

## 7. Discovery Payload for Consensus Device

- [x] 7.1 Extend `DiscoveryPayloadBuilder` in `src/Njord/Egress/DiscoveryPayloadBuilder.cs` with a `BuildConsensus(location, parameters, horizons, forecastDays, mqttOptions, pollInterval, version)` method producing a device-based discovery payload for `njord_{location}_consensus` with the same sensor components as model devices plus diagnostic attributes.
- [x] 7.2 Verify snapshot test in `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` for the consensus discovery payload structure.

## 8. EnrichmentActor

- [x] 8.1 `EnrichmentActor` in `src/Njord/Enrichment/EnrichmentActor.cs`: `WaitingForRefs` state (requests SourceRef from PipelineActor + MqttSinkRef from MqttEgressActor, stashes other messages), `Ready` state (materializes Scan → Where(HasChanged) → BroadcastHub, materializes enabled consumer streams connected to BroadcastHub and MqttSinkRef). Watches both PipelineActor and MqttEgressActor for Terminated → re-requests.
- [x] 8.2 Consensus consumer stream: `snapshotHub.Select(snap => ConsensusResult.Compute(snap, ...)).SelectMany(r => r.ToMqttMessages(...))` with delta publishing closure and Resume supervision, sinking into the MqttSinkRef. Only materialized when `EnrichmentOptions.Consensus.Enabled`.
- [x] 8.3 DI registration in `src/Njord/Program.cs`: register EnrichmentActor with Akka.Hosting, bind `EnrichmentOptions`.
- [x] 8.4 TestKit tests in `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs`: SourceRef request on startup, stashing before refs, transition to Ready, Terminated triggers re-request, disabled consensus skips materialization.

## 9. Discovery Integration

- [x] 9.1 Extend `MqttEgressActor.PublishDiscovery()` to also publish consensus device discovery when `EnrichmentOptions.Consensus.Enabled` is true. Read `EnrichmentOptions` via DI.
- [x] 9.2 Test: verify consensus discovery is published alongside model device discovery when enabled, and skipped when disabled.

## 10. Validation

```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

All existing tests SHALL continue to pass. New tests:
- `src/Njord.Tests/Domain/ModelSnapshotSpec.cs`
- `src/Njord.Tests/Enrichment/ConsensusComputerSpec.cs`
- `src/Njord.Tests/Enrichment/ConsensusResultSpec.cs`
- `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs`
- `src/Njord.Tests/Egress/TopicSchemeSpec.cs` (extended)
- `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` (extended)
- `src/Njord.Tests/Egress/MqttEgressActorSpec.cs` (extended)
