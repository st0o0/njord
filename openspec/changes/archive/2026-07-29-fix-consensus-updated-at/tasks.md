## 1. Proto

- [x] 1.1 Add `google.protobuf.Timestamp consensus_updated_at = 9;` to `GetEnrichmentsResponse` in `protos/njord/v2/weather.proto` (next free field number after `consensus = 8`).
- [x] 1.2 Build `src/Njord.slnx` to confirm the generated C# bindings pick up the new field (`GetEnrichmentsResponse.ConsensusUpdatedAt`, nullable `Timestamp`).

## 2. Service

- [x] 2.1 In `WeatherGrpcService.GetEnrichments` (`src/Njord/Grpc/WeatherGrpcService.cs`), when the mapped `EnrichmentEvent` for the `Consensus` case is produced, also set `response.ConsensusUpdatedAt` from the same `evt.UpdatedAt` (i.e. the same `timeProvider.GetUtcNow()` call already used for that event) instead of discarding it.
- [x] 2.2 Leave `response.ConsensusUpdatedAt` unset when no `Consensus` payload is present in the snapshot (no default/fallback timestamp).

## 3. Tests

- [x] 3.1 In `src/Njord.Tests/Grpc/WeatherGrpcServiceSpec.cs`, add `GetEnrichments_WithConsensusResult_SetsConsensusUpdatedAt` (`sealed`, `[Fact(Timeout = 5000)]`): seed the `EnrichmentSnapshotActor` (or its test double) with a consensus result, call `GetEnrichments`, assert `response.ConsensusUpdatedAt` is set and matches the fake `TimeProvider`'s current time.
- [x] 3.2 Add `GetEnrichments_WithoutConsensusResult_LeavesConsensusUpdatedAtUnset` (`sealed`, `[Fact(Timeout = 5000)]`): seed a snapshot with no consensus result, call `GetEnrichments`, assert `response.ConsensusUpdatedAt` is null/default (has no default `Timestamp`).

## 4. Validation

- [x] 4.1 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj -- -class "Njord.Tests.Grpc.WeatherGrpcServiceSpec"` from `src/` and confirm all cases pass.
- [x] 4.2 Run `dotnet slopwatch` from the repo root after the code changes.
