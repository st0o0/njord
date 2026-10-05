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

Integration tests (`Njord.IntegrationTests`) require Docker for Testcontainers (Mosquitto). They are skipped automatically when Docker is unavailable.

## Run the service

```bash
dotnet run --project Njord/Njord.csproj
```

Uses `appsettings.Development.json` automatically (`ASPNETCORE_ENVIRONMENT=Development`). MQTT is disabled by default.

## Quality gates

```bash
# From the repo root (not src/)
dotnet tool restore
dotnet slopwatch analyze -d . --fail-on warning
```

## Architecture

See [Architecture](/architecture) for the three-zone design, stream pipeline, and project structure. See `AGENTS.md` in the repo root for detailed conventions.
