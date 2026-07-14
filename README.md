# njord

Open-Meteo weather API → MQTT bridge for Home Assistant.

A .NET service (Docker container) built on Akka.NET + Akka.Streams that polls
the [Open-Meteo API](https://open-meteo.com/en/docs) for multiple weather models
per location and publishes everything as Home Assistant entities via
[MQTT Discovery](https://www.home-assistant.io/integrations/mqtt/#mqtt-discovery).

## Quick Start

```yaml
# docker-compose.yml
services:
  njord:
    image: ghcr.io/st0o0/njord:latest
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - Njord__Mqtt__Host=<your-mosquitto-host>
      # - Njord__Mqtt__Username=
      # - Njord__Mqtt__Password=
      - Njord__Locations__0__Name=home
      - Njord__Locations__0__Latitude=47.05
      - Njord__Locations__0__Longitude=8.31
```

```bash
docker compose up -d
```

## Configuration

### Locations

Add multiple locations via indexed environment variables:

```
Njord__Locations__0__Name=home
Njord__Locations__0__Latitude=47.05
Njord__Locations__0__Longitude=8.31
Njord__Locations__1__Name=office
Njord__Locations__1__Latitude=47.37
Njord__Locations__1__Longitude=8.54
```

### Weather Models

Default models: `icon_d2`, `icon_eu`, `icon_global`, `ecmwf_ifs025`,
`gfs_seamless`, `ukmo_global_deterministic_10km`, `meteoswiss_icon_ch1`,
`meteoswiss_icon_ch2`.

Each model creates a device in Home Assistant with sensors per weather parameter
and forecast horizon.

## Build & Test

All commands run from `src/`:

```powershell
dotnet build Njord.slnx
dotnet run --project Njord.Tests/Njord.Tests.csproj   # xUnit v3 via MTP — not `dotnet test`
```

## Structure

```
njord/
├── openspec/            # OpenSpec — specs & change proposals
├── src/
│   ├── Njord/           # Service
│   ├── Njord.Tests/     # Unit tests (xUnit v3, Microsoft.Testing.Platform)
│   ├── Njord.slnx       # Solution
│   ├── Directory.Build.props
│   ├── Directory.Packages.props
│   └── global.json
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## License

Private project — not licensed for redistribution.
