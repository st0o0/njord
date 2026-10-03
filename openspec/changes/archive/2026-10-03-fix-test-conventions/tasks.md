## 1. Add missing timeouts to async tests

- [x] 1.1 Add `[Fact(Timeout = TestTimeouts.Hosted)]` to 7 async methods in `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs` (lines 56, 76, 97, 119, 132, 149, 168)
- [x] 1.2 Add `[Fact(Timeout = TestTimeouts.Hosted)]` to 8 async methods in `src/Njord.Grpc.Tests/OpsGrpcServiceSpec.cs` (lines 55, 76, 88, 99, 110, 130, 143, 156)
- [x] 1.3 Add `[Fact(Timeout = 5000)]` to 7 async methods in `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` (lines 12, 30, 54, 66, 83, 96, 112) — plain async, no TestKit
- [x] 1.4 NjordServiceSetupSpec.cs — kept plain `[Fact]` (only awaits DisposeAsync which has no CancellationToken; xUnit1069 blocks Timeout without token reference)
- [x] 1.5 Removed `async` from sync test in `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs` (line 331) — no awaits

## 2. Fix assertion patterns

- [x] 2.1 Replace `Assert.Single(x); x[0]` with `var item = Assert.Single(x)` in:
  - `src/Njord.Egress.Tests/EgressActorSpec.cs:63-65`
  - `src/Njord.Tests/Configuration/NjordServiceSetupSpec.cs:57-58`
  - `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs:93-94`
  - `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs:51`
  - `src/Njord.Tests/Enrichment/Features/TrendEnrichmentSpec.cs:66`
- [x] 2.2 Add count guards before unguarded index accesses in:
  - `src/Njord.Domain.Tests/Analysis/ConsensusSnapshotSpec.cs:172,191,212,230`
  - `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs:51,180,203,240`
  - `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs:61`
  - `src/Njord.Tests/Enrichment/EnrichmentFeatureContractSpec.cs:148`
  - `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs:65`

## 3. Fix null-forgiving and CancellationToken

- [x] 3.1 Replace `request.RequestUri!.ToString()` with `Assert.NotNull` pattern in `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs:66,291`
- [x] 3.2 `_queue!.OfferAsync()` in Pipeline tests — kept as-is: actor state machine guarantees non-null (guard in TryConnect), `?.` would break state transitions
- [x] 3.3 `DisposeAsync()` does not accept CancellationToken — no change needed

## 4. Remove XML doc comments

- [x] 4.1 Remove `///` XML doc comments from `src/Njord.Core/Configuration/NjordOptions.cs` (6 occurrences)
- [x] 4.2 Remove `///` XML doc comments from `src/Njord.Core/Configuration/MqttOptions.cs` (5 occurrences)
- [x] 4.3 Remove `///` XML doc comments from `src/Njord.Core/Configuration/RequestBudget.cs` (1 occurrence)
- [x] 4.4 Remove `///` XML doc comments from domain records: `src/Njord.Domain/Weather/CycleId.cs`, `WeatherModel.cs`, `ForecastSeries.cs`, `FetchOutcome.cs`
- [x] 4.5 Remove `///` XML doc comments from `src/Njord.Mqtt/Transport/MqttNetPublisher.cs`
- [x] 4.6 Remove `///` XML doc comments from test files: `src/Njord.Tests.Shared/FailingRefProvider.cs`, `src/Njord.Tests/Actors/FailingRefProvider.cs`, `src/Njord.Tests/Mqtt/MqttEgressActorSpec.cs`, `src/Njord.Core.Tests/Actors/StreamConsumerActorSpec.cs`

## 5. Fix public fields in test fakes

- [x] 5.1 Converted `TryCount` to property; `CallCount` kept as field (uses `Interlocked.Increment(ref)`) in `src/Njord.Pipeline.Tests/BudgetThrottleStageSpec.cs`

## 6. Validation

- [x] 6.1 Build and all test suites pass (4 pre-existing Verify golden-master failures in Persistence unrelated to this change)
- [x] 6.2 Slopwatch: 0 issues
- [x] 6.3 dotnet format: clean (fixed 4 pre-existing import ordering issues via `dotnet format`)
