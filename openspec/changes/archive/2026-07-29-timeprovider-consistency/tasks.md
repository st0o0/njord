## 1. TimeProvider consistency

- [x] 1.1 Replace `DateTimeOffset.UtcNow` with `_timeProvider.GetUtcNow()` in `src/Njord/Mqtt/MqttConnectionActor.cs` (lines 81, 165) — TimeProvider already injected
- [x] 1.2 Inject `TimeProvider` into `src/Njord/Grpc/WeatherGrpcService.cs` and replace 4 `DateTimeOffset.UtcNow` usages (lines 104, 165, 213, 225)
- [x] 1.3 Inject `TimeProvider` into `src/Njord/Domain/Analysis/HistoryAnalyzer.cs` and replace fallback `DateTimeOffset.UtcNow` (line 11)
- [x] 1.4 Add tests verifying TimeProvider is used (sealed Spec classes, `[Fact(Timeout = 5000)]`, BDD names) in `src/Njord.Tests/`

## 2. Async actor resolution in PreStart

- [x] 2.1 Replace sync `GetActor` with `GetActorAsync`+`PipeTo` in `src/Njord/Egress/ModelStateActor.cs` (lines 44, 48 in PreStart; lines 96, 100 in reconnect)
- [x] 2.2 Replace sync `GetActor` with `GetActorAsync`+`PipeTo` in `src/Njord/Enrichment/EnrichmentActor.cs` (lines 43, 47 in PreStart; lines 93, 97 in reconnect)
- [x] 2.3 Replace sync `GetActor` with `GetActorAsync`+`PipeTo` in `src/Njord/Mqtt/MqttEgressActor.cs` (lines 53, 57 in PreStart; lines 105, 109 in reconnect)
- [x] 2.4 Replace sync `GetActor` with `GetActorAsync`+`PipeTo` in `src/Njord/Mqtt/DiscoveryActor.cs` (lines 71, 76 in RequestUpstreamRefs)
- [x] 2.5 Replace sync `GetActor` with `GetActorAsync`+`PipeTo` in `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` (lines 31, 63, 64)
- [x] 2.6 Replace sync `GetActor` with `GetActorAsync`+`PipeTo` in `src/Njord/Pipeline/PipelineActor.cs` (line 88)
- [x] 2.7 Add/update tests verifying async resolution works for affected actors

## 3. Enrichment config validation

- [x] 3.1 Implement `IValidateOptions<ConsensusOptions>` in `src/Njord/Configuration/` — validate Method ∈ {Mean, Median, TrimmedMean}, TrimPercent ∈ (0, 0.5) when TrimmedMean
- [x] 3.2 Implement `IValidateOptions<EnergyOptions>` — validate CarnotEfficiency ∈ (0, 1), FlowTemp > 0
- [x] 3.3 Implement `IValidateOptions<HistoryOptions>` — validate SnapshotInterval > 0, RetentionDays > 0, MinSampleSize > 0
- [x] 3.4 Register validators in `src/Njord/Configuration/NjordServiceSetup.cs`
- [x] 3.5 Add validation tests in `src/Njord.Tests/Configuration/EnrichmentOptionsValidationSpec.cs`

## 4. Validation

- [x] 4.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 4.2 Verify zero `DateTimeOffset.UtcNow` in production code via grep
- [x] 4.3 Verify zero sync `GetActor<T>()` in PreStart paths via grep
