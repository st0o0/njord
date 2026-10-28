# Dev Setup

Everything needed to build, test and run the njord service and the full stack with Home Assistant.

## Prerequisites

- **Docker** (Home Assistant and njord together)
- **.NET 10 SDK** (service)
- **Node.js** (docs site and Config Builder)
- **Git**

For the Python integration see [Integration](./integration).

## Clone & Build

```bash
git clone https://github.com/st0o0/njord.git
git clone https://github.com/st0o0/ha-njord.git
cd njord/src
dotnet build Njord.slnx
```

All .NET commands run from `src/`. See [Architecture](/architecture) and `AGENTS.md` for the design and conventions.

## Run Tests

Tests are xUnit v3 on Microsoft.Testing.Platform: use `dotnet run`, not `dotnet test`.

```bash
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "Njord.Core.Tests.SomeSpec"

# all test projects, sequentially
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```

There is one test project per library plus `Njord.Architecture.Tests` and `Njord.IntegrationTests` (whole host in-process via `NjordHost` and a real gRPC channel, about one minute, fixed Akka port 2552). Full-stack checks are the `e2e-test` skill (`.claude/skills/e2e-test/SKILL.md`).

## Run the Service

```bash
dotnet run --project Njord/Njord.csproj
```

`dotnet run` uses the `Development` environment from `launchSettings.json`, so `appsettings.Development.json` is loaded. HTTP is on port 8080, gRPC on 8081.

## Docker Stack

Build a local image and start njord with Home Assistant (the integration is mounted read-only from your `ha-njord` checkout):

```bash
cd njord && docker build -t njord:local .
```

```yaml
services:
  homeassistant:
    image: ghcr.io/home-assistant/home-assistant:stable
    restart: unless-stopped
    ports:
      - "8123:8123"
    volumes:
      - ha-config:/config
      - ./ha-njord/custom_components/njord:/config/custom_components/njord:ro
    environment:
      - TZ=Europe/Zurich

  njord:
    image: njord:local
    restart: unless-stopped
    ports:
      - "8080:8080"
      - "8081:8081"
    volumes:
      - njord-data:/app/data
    environment:
      - Njord__Locations__0__Name=Lucerne
      - Njord__Locations__0__Latitude=47.05
      - Njord__Locations__0__Longitude=8.31
      - Njord__Features__Flood__FloodRisk__Enabled=true

volumes:
  ha-config:
  njord-data:
```

Run `docker compose up -d`; Home Assistant is at `http://localhost:8123`. Python changes show up after an HA restart.
