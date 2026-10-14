# Service Development

The njord service is a .NET 10 application using Akka.NET and Akka.Streams.

**Repository:** [st0o0/njord](https://github.com/st0o0/njord) — `src/` directory

## Build

All commands run from `src/`:

```bash
dotnet build Njord.slnx
```

## Run tests

Tests are xUnit v3 on Microsoft.Testing.Platform — use `dotnet run`, not `dotnet test`:

```bash
# Single test project
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj

# Single test class
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "Njord.Core.Tests.SomeSpec"

# All test projects
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```

There is one test project per library (`Njord.Weather.Tests`, `Njord.Core.Tests`, `Njord.Pipeline.Tests`, ...) plus the host project `Njord.Tests` and the architecture rules in `Njord.Architecture.Tests`. Each test project is its own executable and runs with `dotnet run --project`; none of them needs Docker. Shared fakes and fixtures live in `Njord.Tests.Shared` (not a test project).

End-to-end checks of the full stack (njord, ha-njord and Home Assistant in Docker Compose) are the `e2e-test` skill (`.claude/skills/e2e-test/SKILL.md`), not a test project.

## Run the service

```bash
dotnet run --project Njord/Njord.csproj
```

`dotnet run` picks the `Development` environment from `Njord/Properties/launchSettings.json` (`ASPNETCORE_ENVIRONMENT=Development`), so `appsettings.Development.json` is loaded. The Kestrel endpoints (8080 HTTP, 8081 gRPC) come from `appsettings.json`. MQTT is disabled by default.

## Architecture

See [Architecture](/architecture) for the three-zone design, stream pipeline, and project structure. See `AGENTS.md` in the repo root for detailed conventions.
