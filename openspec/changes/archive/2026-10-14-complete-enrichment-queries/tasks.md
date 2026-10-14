## 1. Failing specs first

- [x] 1.1 `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs`: specs that the read model keeps the latest event of every kind, replaces only the matching kind, and keeps kinds per location; adapt the existing analysis specs to the per-kind result
- [x] 1.2 `src/Njord.Grpc.Tests/AnalysisProtoMappingsSpec.cs`: specs that `LatestAnalysisResult.ToProto()` fills every available field (with `consensus_updated_at` from the consensus event) and leaves missing kinds unset
- [x] 1.3 `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs`: specs for `GetEnrichments` returning all kinds and matching the location case-insensitively, `StreamEnrichments` filtering by location / empty = all / unknown = `NOT_FOUND`, and `StreamForecasts` unknown = `NOT_FOUND`
- [x] 1.4 Run the specs, confirm they fail (compile or assertion) and note the failure messages

## 2. Contract

- [x] 2.1 `src/Njord.Messages/Pipeline/QueryLatestAnalysis.cs`: `LatestAnalysisResult` with one typed nullable event property per kind
- [x] 2.2 `src/Njord.Messages/Pipeline/PipelineEvent.cs`: `IEnrichmentEvent.Location` implemented by every enrichment event

## 3. Read model and gRPC

- [x] 3.1 `src/Njord.Pipeline/ForecastReadModel.cs`: keep a `LatestAnalysisResult` per location and update the slot of the event's kind
- [x] 3.2 `src/Njord.Grpc/Mapping/AnalysisProtoMappings.cs`: `LatestAnalysisResponse.ToProto()` fills every present kind
- [x] 3.3 `src/Njord.Grpc/WeatherGrpcService.cs`: `GetEnrichments` queries the resolved catalog location; `StreamEnrichments` and `StreamForecasts` resolve a non-empty location (unknown = `NOT_FOUND`) and filter by `LocationId`

## 4. Validation

- [x] 4.1 From `src/`: `dotnet build Njord.slnx` with 0 warnings and 0 errors
- [x] 4.2 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` and `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` pass three times in a row; no `*.verified.*` file changes
- [x] 4.3 Full test loop from `AGENTS.md` passes; update "Current total" in `AGENTS.md`
- [x] 4.4 `openspec validate complete-enrichment-queries --no-interactive` passes
