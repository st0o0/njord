## 1. Package reference

- [x] 1.1 Add `Akka.TestKit.Xunit` version entry to `src/Directory.Packages.props`.
- [x] 1.2 Add `<PackageReference Include="Akka.TestKit.Xunit" />` to `src/Njord.Tests/Njord.Tests.csproj`.

## 2. Egress specs → TestKit

- [x] 2.1 Migrate `src/Njord.Tests/Egress/EgressActorSpec.cs` to extend `Akka.TestKit.Xunit.TestKit`. Replace `_system` with `Sys`, remove `IDisposable`.
- [x] 2.2 Migrate `src/Njord.Tests/Egress/ModelStateActorSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IAsyncDisposable` and HOCON shutdown override.

## 3. Mqtt specs → TestKit

- [x] 3.1 Migrate `src/Njord.Tests/Mqtt/MqttConnectionActorSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IAsyncDisposable` and HOCON shutdown override.
- [x] 3.2 Migrate `src/Njord.Tests/Mqtt/DiscoveryActorSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IAsyncDisposable` and HOCON shutdown override.

## 4. Pipeline specs → TestKit

- [x] 4.1 Migrate `src/Njord.Tests/Pipeline/PollPipelineSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IDisposable`.

## 5. Grpc specs

- [x] 5.1 Migrate `src/Njord.Tests/Grpc/ForecastGrpcServiceSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IDisposable`.
- [x] 5.2 Migrate `src/Njord.Tests/Grpc/ConfigGrpcServiceSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IAsyncDisposable`. Drop Guid-based system name (TestKit defaults to "test").
- [x] 5.3 Migrate `src/Njord.Tests/Grpc/ForecastSnapshotActorSpec.cs` to extend `PersistenceTestKit`. Replace `_system` with `Sys`, remove `IDisposable` and HOCON persistence config.
- [x] 5.4 Migrate `src/Njord.Tests/Grpc/EnrichmentSnapshotActorSpec.cs` to extend `PersistenceTestKit`. Replace `_system` with `Sys`, remove `IDisposable` and HOCON persistence config.

## 6. Enrichment specs → TestKit

- [x] 6.1 Migrate `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` to extend `TestKit`. Replace `_system` with `Sys`, remove `IDisposable`.

## 7. Validation

- [x] 7.1 Run `dotnet build Njord.slnx` from `src/` and confirm clean build.
- [x] 7.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` and confirm all tests pass.
