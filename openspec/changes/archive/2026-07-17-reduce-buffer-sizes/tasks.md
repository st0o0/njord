## 1. Buffer Size Reductions

- [x] 1.1 In `src/Njord/Pipeline/PipelineActor.cs`: change `BroadcastHub.Sink<FetchOutcome>(bufferSize: 256)` to `bufferSize: 16`
- [x] 1.2 In `src/Njord/Enrichment/EnrichmentActor.cs`: change `BroadcastHub.Sink<ModelSnapshot>(bufferSize: 64)` to `bufferSize: 8`
- [x] 1.3 In `src/Njord/Egress/EgressActor.cs`: change `BroadcastHub.Sink<EgressEvent>(bufferSize: 64)` to `bufferSize: 16`

## 2. ModelSnapshot Structural Sharing

- [x] 2.1 In `src/Njord/Domain/Weather/ModelSnapshot.cs`: change internal storage from `Dictionary`/`FrozenDictionary` to `ImmutableDictionary`. Update `Empty` to use `ImmutableDictionary<...>.Empty`. Update `Update()` to use `SetItem` instead of `new Dictionary<...>(Entries)`.
- [x] 2.2 Add `using System.Collections.Immutable` and verify `ModelsFor()` and `Entries` property still work (interface is `IReadOnlyDictionary`, unchanged)

## 3. Validation

- [x] 3.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 3.2 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 3.3 Run slopwatch: `dotnet slopwatch` from repo root
