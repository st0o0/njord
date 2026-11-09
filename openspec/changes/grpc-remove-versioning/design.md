## Context

The Grpc project carries v2 and v3 side-by-side. v2 consists of `WeatherGrpcService`, `OpsGrpcService`, 3 proto files, ~9 mapping classes, and dedicated state in `GrpcConsumer`. v3 is the entity-first API (`NjordGrpcService`) with per-OutputKind mappers in `Mapping/V3/`. With v3 stable and ha-njord migrating to it, v2 is dead code. The version prefixes add noise.

## Goals / Non-Goals

**Goals:**
- Remove all v2 code (services, protos, mappings, state, messages)
- Promote v3 to the unversioned standard (no V3 subfolder, no V3 namespace qualifier)
- Move proto from `protos/njord/v3/` to `protos/njord/`, change package to `njord`
- Simplify GrpcConsumer by removing v2 state and the v2 BroadcastHub

**Non-Goals:**
- Changing NjordService behavior or RPCs
- Touching ha-njord
- Removing `CatalogResult` from `Njord.Messages` (it may serve other purposes later)

## Decisions

### 1. Proto package rename: `njord.v3` to `njord`

Move `protos/njord/v3/njord.proto` to `protos/njord/njord.proto`. Change `package njord.v3` to `package njord` and `csharp_namespace` from `Njord.Grpc.V3` to `Njord.Grpc`. Update the csproj Protobuf include. This is a wire-breaking change but acceptable at 0.x.

Alternative: keep proto package as `njord.v3` internally but rename C# namespace only. Rejected because it leaves the version in the wire format permanently, contradicting the goal.

### 2. Mapping folder flattening

Move all 9 files from `Mapping/V3/` to `Mapping/`. Change namespace from `Njord.Grpc.Mapping.V3` to `Njord.Grpc.Mapping`. The v2-only mapping files in `Mapping/` are deleted first to avoid naming conflicts (there are none -- v2 and v3 mappers have different names).

### 3. v2-only mapping files: delete all

All files in `Mapping/` (non-V3) are v2-only:
- `WindowMappingExtensions.cs` -- maps to `V2.TimeWindow`
- `WireEnumMappingExtensions.cs` -- maps to `V2.PollPhase`, `V2.Domain`
- `CatalogProtoMappingExtensions.cs` -- maps to `V2.GetCatalogResponse`
- `OpsProtoMappingExtensions.cs` -- maps to `V2.ModelStatus`
- `AlertResultMappingExtensions.cs` -- v2 per-feature alert mapping
- `FeatureResultMappingExtensions.cs` -- v2 per-feature enrichment mapping
- `FeatureEnumMappingExtensions.cs` -- v2 feature enum mapping
- `EnrichmentEventMappingExtensions.cs` -- v2 `EnrichmentEvent` mapping
- `DomainDataProtoMappingExtensions.cs` -- v2 `DomainDataUpdate` mapping
- `ParamFieldNames.cs` -- only consumer is `CatalogMessageMappingExtensions` (v2)
- `CatalogMessageMappingExtensions.cs` -- only consumer is `WeatherGrpcService` (v2)

### 4. GrpcConsumer simplification

Remove v2 state fields (`DomainData`, `Results`) from `GrpcConsumerState`. Remove v2 message handlers (`QueryDomainData`, `QueryLatestAnalysis`, `RequestGrpcStream`). Remove the first BroadcastHub (PipelineEvent hub, used only by v2 `RequestGrpcStream`). Simplify `OnPipelineEvent` to only feed entity state. The state becomes just `EntityStates` + `StaleEntities`.

### 5. v2-only messages in Njord.Messages

Remove message types only used by v2 gRPC services:
- `RequestGrpcStream` / `RequestGrpcStreamCompleted` / `RequestGrpcStreamFailed`
- `QueryDomainData` / `DomainDataResult` / `DomainDataNotAvailable`
- `QueryLatestAnalysis` / `LatestAnalysisResult` / `LatestAnalysisNotAvailable`

Check each for non-gRPC consumers before deleting.

### 6. Test cleanup mirrors production

Delete all v2 test files. Move `Mapping/V3/` tests to `Mapping/`. Update namespaces. Delete the Verify snapshot for v2 enrichment events.

## Risks / Trade-offs

- [Wire breaking] Proto package rename breaks existing ha-njord clients. Mitigation: ha-njord is co-designed and will be updated in lockstep.
- [Missed reference] A v2 message type might be used outside Grpc. Mitigation: grep before deleting each message; the compiler catches survivors.
- [Large diff] Many file deletions in one change. Mitigation: the deletions are mechanical and the compiler validates completeness.
