## 1. Domain model

- [x] 1.1 Create `SensorKind` enum and `SensorKindMetadata` record (unit, min, max, aggregation strategy) in `src/Njord/Domain/Sensors/`
- [x] 1.2 Create `SensorReading` record (Kind, Location, Source, Value, MeasuredAt) in `src/Njord/Domain/Sensors/`
- [x] 1.3 Create `AggregatedReading` record (Value, SourceCount, NewestMeasuredAt) and `SensorSnapshot` record with `Get(SensorKind)` method in `src/Njord/Domain/Sensors/`
- [x] 1.4 Write tests for `SensorKindMetadata` validation and `SensorSnapshot.Get` in `src/Njord.Tests/Domain/Sensors/SensorSnapshotSpec.cs`

## 2. SensorHub actor

- [x] 2.1 Define actor messages: `UpdateReading`, `GetSnapshot`, `SensorSnapshotResponse` in `src/Njord/Domain/Sensors/`
- [x] 2.2 Implement `SensorHubActor` in `src/Njord/Sensors/SensorHubActor.cs` — store readings, aggregate on query, periodic expiry timer
- [x] 2.3 Write tests for SensorHubActor: store/retrieve, multi-source aggregation (Average, Sum, Latest), staleness expiry, plausibility rejection in `src/Njord.Tests/Sensors/SensorHubActorSpec.cs`

## 3. Configuration

- [x] 3.1 Create `SensorOptions` class (Enabled, StalenessSeconds) in `src/Njord/Configuration/SensorOptions.cs`
- [x] 3.2 Create `SensorOptionsValidator` in `src/Njord/Configuration/` and register in DI
- [x] 3.3 Register SensorHubActor in `NjordActorSetup` via Akka.Hosting
- [x] 3.4 Add Sensors section to `appsettings.Example.json` and `appsettings.Development.json`
- [x] 3.5 Write validation tests in `src/Njord.Tests/Configuration/SensorOptionsValidationSpec.cs`

## 4. gRPC SensorService

- [x] 4.1 Create `protos/njord/v2/sensor.proto` with `SensorKind` enum, `SensorReading` message, `PushResponse` message, `SensorService` (Push + StreamPush RPCs)
- [x] 4.2 Implement `SensorGrpcService` in `src/Njord/Grpc/SensorGrpcService.cs` — validate kind, location, plausibility; forward to SensorHub; handle empty source
- [x] 4.3 Map `SensorService` endpoint in gRPC endpoint configuration
- [x] 4.4 Write tests for SensorGrpcService: valid reading, unknown kind, unknown location, plausibility rejection, empty source normalization in `src/Njord.Tests/Grpc/SensorGrpcServiceSpec.cs`

## 5. Enrichment interface change

- [x] 5.1 Add `SensorSnapshot?` parameter to `IStatelessEnrichment.Compute(ConsensusSnapshot, SensorSnapshot?)`
- [x] 5.2 Add `SensorSnapshot?` parameter to `IStatefulEnrichment.Compute(ConsensusSnapshot, ConsensusSnapshot?, SensorSnapshot?)`
- [x] 5.3 Update all implementations: `AlertEnrichment`, `DerivedEnrichment`, `IndexEnrichment`, `TrendEnrichment` (pass through, ignore sensors)
- [x] 5.4 Update `HistoryEnrichment` (IActorEnrichment — no signature change needed, but verify)
- [x] 5.5 Update all enrichment tests to pass `null` as SensorSnapshot where needed

## 6. Wire SensorHub into EnrichmentActor

- [x] 6.1 Add SensorHub dependency resolution in `EnrichmentActor.ResolveDependencies` and `ConfigureWaitingForRefs`
- [x] 6.2 Modify `BuildConsensusInlineFlow` to accept SensorHub ActorRef and pull `SensorSnapshot` per consensus via `Ask` with 1s timeout
- [x] 6.3 Pass `SensorSnapshot?` through to `ComputeAll` and into each enrichment's `Compute` call
- [x] 6.4 Update `EnrichmentActorSpec` tests to verify SensorHub integration

## 7. Index enrichment sensor fallback

- [x] 7.1 Update `IndexEnrichment.Compute` to extract `IndoorTemperature` from `SensorSnapshot` and override `ResolvedPreferences.IndoorTemp` when available
- [x] 7.2 Write tests for the fallback chain: sensor → config → default in `src/Njord.Tests/Enrichment/Features/IndexEnrichmentSpec.cs`

## 8. Documentation

- [x] 8.1 Add sensor configuration section to `docs/configuration/`
- [x] 8.2 Update `docs/architecture.md` to mention the SensorHub data flow
- [x] 8.3 Update `CLAUDE.md` to mention the SensorHub and gRPC sensor input
- [x] 8.4 Update `README.md` feature list

## 9. Verification

- [x] 9.1 Run `dotnet build Njord.slnx` from `src/` — must compile clean
- [x] 9.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests must pass
- [x] 9.3 Run `dotnet slopwatch` from repo root
- [x] 9.4 Run `dotnet format` whitespace check from `src/`
