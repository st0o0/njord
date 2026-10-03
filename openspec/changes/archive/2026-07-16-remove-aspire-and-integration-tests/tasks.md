## 1. Inline ServiceDefaults

- [x] 1.1 Move Serilog setup from `ServiceDefaults.Extensions.AddNjordTelemetry()` inline into `src/Njord/Program.cs`
- [x] 1.2 Move health/alive endpoint setup from `ServiceDefaults.Extensions.MapDefaultEndpoints()` inline into `src/Njord/Configuration/NjordApplicationSetup.cs`
- [x] 1.3 Remove `using Njord.ServiceDefaults` from `Program.cs` and `NjordApplicationSetup.cs`
- [x] 1.4 Remove `ProjectReference` to `Njord.ServiceDefaults` from `src/Njord/Njord.csproj`

## 2. Remove Projects from Solution

- [x] 2.1 Remove `Njord.AppHost`, `Njord.ServiceDefaults`, `Njord.Tests.Integration`, and `Njord.Tests.Integration.E2E` from `src/Njord.slnx`

## 3. Delete Project Directories

- [x] 3.1 Delete `src/Njord.AppHost/` directory
- [x] 3.2 Delete `src/Njord.ServiceDefaults/` directory
- [x] 3.3 Delete `src/Njord.Tests.Integration/` directory
- [x] 3.4 Delete `src/Njord.Tests.Integration.E2E/` directory

## 4. Cleanup

- [x] 4.1 Remove `Aspire.Hosting.PostgreSQL` and `Aspire.Hosting.Testing` from `src/Directory.Packages.props`
- [x] 4.2 Remove `MosquittoHelper` reference from `Njord.Tests.Shared` if it exists and is only used by integration tests
- [x] 4.3 Update `CLAUDE.md` — remove integration and E2E test `dotnet run` commands, remove Aspire AppHost references

## 5. Validation

- [x] 5.1 Build the solution: `dotnet build Njord.slnx` from `src/`
- [x] 5.2 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 5.3 Run slopwatch: `dotnet slopwatch` from repo root
