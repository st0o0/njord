## Why

The gRPC layer still carries v2/v3 version prefixes everywhere (namespaces, folders, proto packages) even though v3 is the only active version and v2 is dead code. Removing the versioning simplifies the codebase and makes v3 the canonical, unversioned API going forward.

## What Changes

- **BREAKING**: Delete v2 gRPC services (`WeatherGrpcService`, `OpsGrpcService`) and all v2 mappings
- **BREAKING**: Delete `protos/njord/v2/` (3 proto files: `common.proto`, `ops.proto`, `weather.proto`)
- **BREAKING**: Change proto package from `njord.v3` to `njord`, `csharp_namespace` from `Njord.Grpc.V3` to `Njord.Grpc`
- Move proto from `protos/njord/v3/njord.proto` to `protos/njord/njord.proto`
- Move `Mapping/V3/*.cs` to `Mapping/`, change namespace from `Njord.Grpc.Mapping.V3` to `Njord.Grpc.Mapping`
- Remove v2 state tracking from `GrpcConsumer` and `GrpcConsumerState`
- Remove v2 endpoint registration from `GrpcEndpointExtensions`/`GrpcSetup`
- Delete all v2-only test files and move `Mapping/V3/` tests to `Mapping/`

## Non-goals

- Changing gRPC service behavior or RPCs (NjordService stays as-is)
- Changing the entity-first architecture or ManifestBuilder
- Adding new features or endpoints
- Touching ha-njord (that's a separate repo)

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `v3-proto`: Proto moves from `protos/njord/v3/` to `protos/njord/`, package changes from `njord.v3` to `njord`
- `grpc-mapping-layer`: V3 subfolder merges into Mapping root, v2 mappings deleted

## Impact

- **Proto wire format**: Package name changes from `njord.v3` to `njord` (breaking for existing ha-njord clients)
- **Njord.Grpc**: Major file deletions and moves, namespace changes
- **Njord.Grpc.Tests**: Parallel deletions and moves
- **Njord.Grpc.csproj**: Proto include paths change
- **GrpcConsumer**: v2 state fields and v2 broadcast hub removed
- **No budget impact**: Pure cleanup, no polling or API changes
