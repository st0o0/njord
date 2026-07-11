# njord

Kachelmann weather API → MQTT bridge for Home Assistant.

A .NET service (Docker container) built on Akka.NET + Akka.Streams that polls the
[Kachelmannwetter API](https://api.kachelmannwetter.com) for multiple weather models,
computes a consensus forecast, and publishes everything as Home Assistant entities
via MQTT Discovery.

## Structure

```
njord/
├── openspec/            # OpenSpec — specs & change proposals (spec-driven workflow)
├── src/
│   ├── Njord/           # Service
│   ├── Njord.Tests/     # Unit tests (xUnit v3, Microsoft.Testing.Platform)
│   ├── Njord.slnx       # Solution
│   ├── Directory.Build.props
│   ├── Directory.Packages.props
│   └── global.json
└── README.md
```

## Build & Test

All commands run from `src/` (where `global.json` lives):

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Tests/Njord.Tests.csproj   # xUnit v3 via MTP — not `dotnet test`
```
