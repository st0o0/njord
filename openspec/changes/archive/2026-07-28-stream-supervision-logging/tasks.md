## 1. Shared stream supervision decider

- [x] 1.1 Create `src/Njord/Pipeline/StreamSupervision.cs` with a `LoggingDecider(ILogger)` static factory that returns an Akka.Streams `Decider`: log every exception at Warning level, return `Resume` for `AskTimeoutException`, `TaskCanceledException`, `OperationCanceledException`, `TimeoutException`, `HttpRequestException`; return `Stop` for everything else
- [x] 1.2 Add unit tests in `src/Njord.Tests/Pipeline/StreamSupervisionSpec.cs`: verify Resume for each transient type, Stop for NullReferenceException/InvalidOperationException, and that the logger is called for all cases

## 2. Replace blanket deciders in all stream graphs

- [x] 2.1 `src/Njord/Pipeline/PipelineActor.cs` (lines 103, 115): replace both `_ => Resume` with `StreamSupervision.LoggingDecider(_logger)`
- [x] 2.2 `src/Njord/Egress/ModelStateActor.cs` (line 143): replace with `StreamSupervision.LoggingDecider(_logger)`
- [x] 2.3 `src/Njord/Enrichment/EnrichmentActor.cs` (line 169): replace with `StreamSupervision.LoggingDecider(_logger)`
- [x] 2.4 `src/Njord/Mqtt/MqttEgressActor.cs` (line 124): replace with `StreamSupervision.LoggingDecider(_logger)`
- [x] 2.5 `src/Njord/Mqtt/MqttConnectionActor.cs` (line 133): replace with `StreamSupervision.LoggingDecider(_logger)`
- [x] 2.6 `src/Njord/Grpc/GrpcSnapshotConsumerActor.cs` (line 69): replace with `StreamSupervision.LoggingDecider(_logger)`
- [x] 2.7 `src/Njord/Enrichment/Features/HistoryEnrichment.cs` (line 77): replace with `StreamSupervision.LoggingDecider(_logger)` + inject ILogger<HistoryEnrichment>

## 3. MQTT Enabled default fix

- [x] 3.1 `src/Njord/Configuration/NjordServiceSetup.cs` (line 53): change `GetValue("Enabled", true)` to `GetValue("Enabled", false)`
- [x] 3.2 Update test: `NjordServiceSetupSpec.BuildProvider()` now explicitly sets `Njord:Mqtt:Enabled=true`

## 4. Validation

- [x] 4.1 Run full test suite: 587 tests, 0 failures
- [x] 4.2 Run `dotnet slopwatch`: no new issues (1 pre-existing SW005)
- [x] 4.3 Build: zero errors, only pre-existing warnings
