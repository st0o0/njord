## 1. Domain — Add ComputedAt to ConsensusSnapshot and ConsensusResult

- [x] 1.1 Add `ComputedAt` (`DateTimeOffset`) to the `ConsensusSnapshot` record in `src/Njord/Domain/Analysis/ConsensusSnapshot.cs`. Set it from `timeProvider.GetUtcNow()` inside `Compute`. Thread it through the record constructor.
- [x] 1.2 Add `ComputedAt` (`DateTimeOffset?`) to the `ConsensusResult` record in `src/Njord/Domain/Analysis/ConsensusResult.cs`. Add `[JsonProperty("computedAt")]`. The single-parameter convenience constructor sets it to `null`.
- [x] 1.3 Update `ConsensusSnapshotSpec` in `src/Njord.Tests/Domain/Analysis/ConsensusSnapshotSpec.cs`: assert `ComputedAt` equals the `FakeTimeProvider` value in the existing `Compute_produces_snapshot_with_hourly_and_daily_facets` test and add a dedicated `ComputedAt_matches_time_provider` test.

## 2. Enrichment Pipeline — Propagate timestamp through egress

- [x] 2.1 Add optional `DateTimeOffset? UpdatedAt` parameter (default `null`) to `EgressEvent.EnrichmentUpdate` in `src/Njord/Egress/EgressEvent.cs`.
- [x] 2.2 In `EnrichmentActor.ComputeAll` (`src/Njord/Enrichment/EnrichmentActor.cs`), populate `ConsensusResult.ComputedAt` from `consensus.ComputedAt` and pass it as `UpdatedAt` on the `EgressEvent.EnrichmentUpdate` for consensus events.

## 3. gRPC — Use stored timestamp for consensus events

- [x] 3.1 In `WeatherGrpcService.GetEnrichments` (`src/Njord/Grpc/WeatherGrpcService.cs`), when the result is a `ConsensusResult`, use `ConsensusResult.ComputedAt ?? timeProvider.GetUtcNow()` as the `updatedAt` argument to `EnrichmentProtoMapper.MapToEvent` instead of unconditionally using `timeProvider.GetUtcNow()`.
- [x] 3.2 In `WeatherGrpcService.StreamEnrichments` (`src/Njord/Grpc/WeatherGrpcService.cs`), use `update.UpdatedAt ?? timeProvider.GetUtcNow()` as the `updatedAt` argument to `EnrichmentProtoMapper.MapToEvent`.
- [x] 3.3 Update `WeatherGrpcServiceSpec` (`src/Njord.Tests/Grpc/WeatherGrpcServiceSpec.cs`) or add assertions verifying that the consensus `updated_at` / `consensus_updated_at` reflects the computation time, not the query time.

## 4. Persistence — Ensure ComputedAt survives snapshot round-trips

- [x] 4.1 Verify that the existing `EnrichmentSnapshotMapping` serialization path (Newtonsoft.Json with `[JsonProperty]`) correctly handles the new `ComputedAt` property on `ConsensusResult`. No DTO changes should be needed since the result is serialized as inner JSON.
- [x] 4.2 Add a round-trip test in `src/Njord.Tests/Persistence/EnrichmentResultSerializationSpec.cs` asserting that `ConsensusResult.ComputedAt` survives `ToDto` → `ToDomain`. Add a second test deserializing a legacy JSON payload without `computedAt` and asserting it recovers as `null`.

## 5. Validation

- [x] 5.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj` from the `src/` directory. All existing and new tests must pass.
- [x] 5.2 Run `dotnet build src/Njord.slnx` to verify no compilation errors.
