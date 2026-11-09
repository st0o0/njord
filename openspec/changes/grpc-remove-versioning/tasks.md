## 1. Delete v2 protos and update csproj

- [x] 1.1 Delete `protos/njord/v2/` (common.proto, ops.proto, weather.proto)
- [x] 1.2 Move `protos/njord/v3/njord.proto` to `protos/njord/njord.proto`
- [x] 1.3 Delete empty `protos/njord/v3/` directory
- [x] 1.4 In `njord.proto`: change `package njord.v3` to `package njord`, `csharp_namespace` to `Njord.Grpc.Proto`
- [x] 1.5 In `Njord.Grpc.csproj`: remove v2 Protobuf include, update path to `protos\njord\*.proto`, set `GrpcServices="Both"`

## 2. Delete v2 services and v2-only mappings

- [x] 2.1 Delete `WeatherGrpcService.cs` and `OpsGrpcService.cs`
- [x] 2.2 Delete v2-only mapping files (11 files)
- [x] 2.3 In `GrpcEndpointExtensions.cs`: remove v2 service registrations

## 3. Move V3 mappings to Mapping root

- [x] 3.1 Move all 9 files from `Mapping/V3/` to `Mapping/`
- [x] 3.2 Change namespace from `Njord.Grpc.Mapping.V3` to `Njord.Grpc.Mapping`
- [x] 3.3 Delete empty `Mapping/V3/` directory

## 4. Update references to use Proto namespace

- [x] 4.1 In `NjordGrpcService.cs`: use `Proto.` prefix for proto types
- [x] 4.2 In `GrpcConsumer.cs`: use `Proto.` prefix, remove v2 usings
- [x] 4.3 In `ManifestBuilder.cs`: update aliases to `Njord.Grpc.Proto.*`
- [x] 4.4 In `EntityStreamMessages.cs`: update to `Proto.EntityEvent`
- [x] 4.5 In all mapping files: use `Proto.` prefix for proto types, update using aliases

## 5. Simplify GrpcConsumer (remove v2 state)

- [x] 5.1 In `GrpcConsumerState.cs`: remove `DomainData` and `Results` fields
- [x] 5.2 In `GrpcConsumerStateExtensions.cs`: remove v2 methods (`Apply`, `GetDomainData`, `GetLatestResults`)
- [x] 5.3 In `GrpcConsumer.cs`: remove v2 handlers and PipelineEvent BroadcastHub
- [x] 5.4 In `EntityState.cs`: verified no v2 dependencies

## 6. Remove v2-only messages from Njord.Messages

- [x] 6.1 Grepped for non-Grpc consumers -- all v2-only
- [x] 6.2 Delete `GrpcStreamMessages.cs`
- [x] 6.3 Delete `QueryDomainData.cs`
- [x] 6.4 Delete `QueryLatestAnalysis.cs`

## 7. Test cleanup

- [x] 7.1 Delete v2 test files (10 files)
- [x] 7.2 Delete v2 mapping tests (3 files)
- [x] 7.3 Move V3 test files to `Mapping/`, update namespaces
- [x] 7.4 Update Proto references in test files
- [x] 7.5 Delete `ParamFieldNamesTests.cs`
- [x] 7.6 Delete v2-only consumer test files (`GrpcConsumerTests.cs`, `GrpcConsumerStreamReplayTests.cs`, `GrpcConsumerStateTests.cs`)
- [x] 7.7 Rewrite integration tests for v3 API
- [x] 7.8 Fix architecture test: exclude `Njord.Grpc.Proto` from sealed check

## 8. Build, format, verify

- [x] 8.1 `dotnet build Njord.slnx` -- 0 errors, 0 warnings
- [x] 8.2 `dotnet format Njord.slnx --verify-no-changes` -- clean
- [x] 8.3 Run all test projects -- all pass (1621 total)
- [x] 8.4 Update AGENTS.md/CLAUDE.md: remove v2/v3 references
