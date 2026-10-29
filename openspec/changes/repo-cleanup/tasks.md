## 1. Remove unused NuGet packages

- [x] 1.1 Remove `Testcontainers` from `src/Directory.Packages.props`
- [x] 1.2 Remove `Akka.Persistence.TestKit.Xunit` PackageReference from `src/Njord.Budget.Tests/Njord.Budget.Tests.csproj`, `src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`, `src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`, `src/Njord.Polling.Tests/Njord.Polling.Tests.csproj`; remove version entry from `src/Directory.Packages.props`
- [x] 1.3 Remove `Newtonsoft.Json` PackageReference from `src/Njord.Persistence/Njord.Persistence.csproj`

## 2. Delete dead production code

- [x] 2.1 Delete `src/Njord/Configuration/LogLevelMappingExtensions.cs`
- [x] 2.2 Delete `src/Njord.Core/Horizon/Horizon.cs`, `src/Njord.Core/Horizon/HorizonKind.cs`, and the `src/Njord.Core/Horizon/` directory if empty

## 3. Delete dead test code

- [x] 3.1 Delete `src/Njord.Core.Tests/Horizon/HorizonTests.cs` and the `src/Njord.Core.Tests/Horizon/` directory if empty
- [x] 3.2 Delete `src/Njord.Tests.Shared/AlertScenario.cs` and `src/Njord.Tests.Shared/ForecastPointBuilder.cs`
- [x] 3.3 Remove the `SilentActor` inner class from `src/Njord.IntegrationTests/Grpc/GrpcChannelTests.cs`

## 4. Delete stale files

- [x] 4.1 Delete `TODO.md` from repo root
- [x] 4.2 Delete `tools/model-probes/` directory

## 5. Fix file naming

- [x] 5.1 Rename `src/Njord.Messages/Pipeline/TriggerTargetPoll.cs` to `TriggerLocationPoll.cs`

## 6. Docs navigation

- [x] 6.1 In `docs/.vitepress/config.ts`, replace the "Dev" entry in the top nav with "Builder" linking to `/builder`

## 7. Validation

- [x] 7.1 Run `dotnet build Njord.slnx` from `src/` (must compile clean)
- [x] 7.2 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 7.3 Run all test projects sequentially from `src/`:
  - `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`
  - `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`
  - `dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj`
  - `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj`
  - `dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj`
  - `dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj`
  - `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`
  - `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj`
  - `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`
  - `dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj`
  - `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
- [x] 7.4 Update AGENTS.md/CLAUDE.md if any removed items are referenced there (Horizon in solution structure, Testcontainers in skill routing)
