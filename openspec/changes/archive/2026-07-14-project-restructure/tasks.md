## 1. Domain namespace restructure

- [x] 1.1 Create `src/Njord/Domain/Weather/` folder. Move `CycleId.cs`, `DailyForecastPoint.cs`, `DailyForecastSeries.cs`, `ForecastDataHash.cs`, `ForecastPoint.cs`, `ForecastSeries.cs`, `ModelForecast.cs`, `ModelSnapshot.cs`, `ParameterDef.cs`, `ParameterRegistry.cs`, `WeatherModel.cs` from `src/Njord/Domain/` to `src/Njord/Domain/Weather/`. Update namespace from `Njord.Domain` to `Njord.Domain.Weather` in each file.
- [x] 1.2 Create `src/Njord/Domain/Analysis/` folder. Move `ConsensusComputer.cs`, `ConsensusResult.cs`, `DerivedComputer.cs`, `DerivedResult.cs`, `AlertEvaluator.cs`, `AlertResult.cs`, `TrendAnalyzer.cs`, `TrendResult.cs`, `EnergyForecaster.cs`, `EnergyResult.cs`, `IndexScorer.cs`, `IndexResult.cs`, `HistoryAnalyzer.cs`, `HistoryResult.cs`, `ForecastHistory.cs` from `src/Njord/Enrichment/` to `src/Njord/Domain/Analysis/`. Update namespace from `Njord.Enrichment` to `Njord.Domain.Analysis`.
- [x] 1.3 Fix all `using` statements across the codebase to reference the new `Njord.Domain.Weather` and `Njord.Domain.Analysis` namespaces. Build and verify compilation: `dotnet build src/Njord.slnx`.

## 2. Domain test restructure

- [x] 2.1 Create `src/Njord.Tests/Domain/Weather/` folder. Move existing domain tests (`CycleId` through `WeatherModelSpec`) into it. Update namespaces to `Njord.Tests.Domain.Weather`.
- [x] 2.2 Create `src/Njord.Tests/Domain/Analysis/` folder. Move analysis-related tests (`ConsensusComputerSpec`, `ConsensusResultSpec`, `DerivedComputerSpec`, `DerivedResultSpec`, `AlertEvaluatorSpec`, `AlertResultSpec`, `TrendAnalyzerSpec`, `TrendResultSpec`, `EnergyForecasterSpec`, `EnergyResultSpec`, `IndexScorerSpec`, `IndexResultSpec`, `HistoryAnalyzerSpec`, `HistoryResultSpec`) from `src/Njord.Tests/Enrichment/` into it. Update namespaces to `Njord.Tests.Domain.Analysis`.
- [x] 2.3 Build and run tests to verify the domain restructure is green: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`.

## 3. Remove ToMqttMessages from Result records

- [x] 3.1 Remove `ToMqttMessages()` method from `ConsensusResult`, `AlertResult`, `DerivedResult`, `TrendResult`, `IndexResult`, `EnergyResult`, `HistoryResult` in `src/Njord/Domain/Analysis/`. Remove any `using Njord.Mqtt` or `using Njord.Egress` from these files.
- [x] 3.2 Add corresponding static methods to `StatePayloadBuilder` in `src/Njord/Egress/` (will move to `Mqtt/` in phase 5): `FromConsensus()`, `FromAlerts()`, `FromDerived()`, `FromTrends()`, `FromIndices()`, `FromEnergy()`, `FromHistory()` — each taking the domain result and returning `IReadOnlyList<MqttMessage>`.
- [x] 3.3 Update `ToMqttMessages` tests: move assertions to `StatePayloadBuilder` tests in `src/Njord.Tests/Egress/StatePayloadBuilderSpec.cs` (will move to `Mqtt/` in phase 5). Remove `ToMqttMessages` test methods from the result spec files.
- [x] 3.4 Build and run tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`.

## 4. Publisher protocol and EgressActor

- [x] 4.1 Create `src/Njord/Egress/EgressActor.cs` — publisher-agnostic router. Maintains `HashSet<IActorRef>` of registered publishers. Handles `RegisterPublisher`, `UnregisterPublisher`, `PublishStateResult`. Watches registered publishers, auto-unregisters on `Terminated`. Forwards `PublishStateResult` to all registered publishers.
- [x] 4.2 Update `src/Njord/Egress/EgressMessages.cs` — add `RegisterPublisher`, `UnregisterPublisher`, `PublishStateResult` message records. Remove old MQTT-specific messages that will move to `Mqtt/`.
- [x] 4.3 Write `src/Njord.Tests/Egress/EgressActorSpec.cs` — test registration, broadcast, auto-unregister on Terminated, duplicate registration, unregister unknown publisher.
- [x] 4.4 Build and run tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`.

## 5. Mqtt namespace and actor split

- [x] 5.1 Create `src/Njord/Mqtt/` folder. Move `MqttMessage.cs`, `TopicScheme.cs`, `DiscoveryPayloadBuilder.cs`, `StatePayloadBuilder.cs` from `src/Njord/Egress/` to `src/Njord/Mqtt/`. Update namespace from `Njord.Egress` to `Njord.Mqtt`.
- [x] 5.2 Create `src/Njord/Mqtt/Transport/` folder. Move `IMqttConnection.cs`, `IMqttTransport.cs`, `MqttNetPublisher.cs` from `src/Njord/Egress/` to `src/Njord/Mqtt/Transport/`. Update namespace to `Njord.Mqtt.Transport`.
- [x] 5.3 Create `src/Njord/Mqtt/MqttConnectionActor.cs` — extract connection lifecycle from `MqttEgressActor`: `Connect()`, `ScheduleReconnect()`, `OnConnectedAsync()`, LWT publishing, MergeHub materialization, `RequestMqttSink`/`MqttSinkResponse` protocol. Owns `IMqttConnection` and `IMqttTransport`.
- [x] 5.4 Create `src/Njord/Mqtt/MqttPublisherActor.cs` — registers with `EgressActor` via `RegisterPublisher` on startup. Requests `SinkRef<MqttMessage>` from `MqttConnectionActor`. On `PublishStateResult`, transforms domain results via `StatePayloadBuilder.From*()` methods and pushes `MqttMessage` instances into the MergeHub sink. Maintains delta-publishing cache. Also materializes the pipeline SourceRef consumer (FetchOutcome → per-horizon state messages).
- [x] 5.5 Create `src/Njord/Mqtt/DiscoveryActor.cs` — requests `SinkRef<MqttMessage>` from `MqttConnectionActor`. Subscribes to HA status topic. On connect notification and HA birth, publishes discovery config payloads for all devices. No-op when `DiscoveryEnabled` is false.
- [x] 5.6 Delete `src/Njord/Egress/MqttEgressActor.cs` and `src/Njord/Egress/EgressServiceCollectionExtensions.cs`. Remove any now-empty files from `src/Njord/Egress/`.
- [x] 5.7 Fix all `using` statements across the codebase for the `Njord.Mqtt` and `Njord.Mqtt.Transport` namespaces. Build: `dotnet build src/Njord.slnx`.

## 6. Mqtt test restructure

- [x] 6.1 Create `src/Njord.Tests/Mqtt/` folder. Move `TopicSchemeSpec.cs`, `DiscoveryPayloadBuilderSpec.cs`, `StatePayloadBuilderSpec.cs`, `MqttEgressIntegrationSpec.cs` from `src/Njord.Tests/Egress/` to `src/Njord.Tests/Mqtt/`. Update namespaces to `Njord.Tests.Mqtt`.
- [x] 6.2 Write `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs` — test connect, reconnect, LWT online/offline, SinkRef vending.
- [x] 6.3 Write `src/Njord.Tests/Mqtt/MqttPublisherActorSpec.cs` — test registration with EgressActor, domain result → MqttMessage transformation, delta publishing.
- [x] 6.4 Write `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` — test discovery publishing on connect, re-publish on HA birth, no-op when disabled.
- [x] 6.5 Update `MqttEgressIntegrationSpec` to use the new actor topology (MqttConnectionActor + MqttPublisherActor + DiscoveryActor).

## 7. EnrichmentActor rewire

- [x] 7.1 Update `src/Njord/Enrichment/EnrichmentActor.cs` — replace all `result.ToMqttMessages(baseTopic)` calls with `egressActor.Tell(new PublishStateResult(location, result))`. Resolve `EgressActor` via `Context.GetActor<EgressActor>()`. Remove `using Njord.Mqtt` / `using Njord.Egress` MqttMessage references.
- [x] 7.2 Update `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` — verify enrichment streams send `PublishStateResult` to `EgressActor` instead of producing `MqttMessage`.
- [x] 7.3 Build and run tests: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`.

## 8. Actor registration and wiring

- [x] 8.1 Update `src/Njord/Configuration/NjordActorSystemSetup.cs` — register `EgressActor`, `MqttConnectionActor`, `MqttPublisherActor`, `DiscoveryActor` via `WithResolvableActors`. Remove `MqttEgressActor` registration.
- [x] 8.2 Update `src/Njord/Configuration/NjordServiceSetup.cs` — update DI registrations to match new namespaces and types.
- [x] 8.3 Update `src/Njord.Tests/Configuration/NjordActorSystemSetupSpec.cs` and `NjordServiceSetupSpec.cs` to reflect new actor registrations.

## 9. Validation

- [x] 9.1 Build: `dotnet build src/Njord.slnx`
- [x] 9.2 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 9.3 Verify no remaining references to old namespaces: grep for `Njord.Egress.MqttEgressActor`, `Njord.Egress.TopicScheme`, `Njord.Egress.MqttMessage`, `Njord.Egress.DiscoveryPayloadBuilder` — should return zero hits.

## 10. Validation commands

```powershell
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```
