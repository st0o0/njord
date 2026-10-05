# Dev Setup

This guide gets a complete development environment running: the njord service, Home Assistant, and the custom integration — all connected.

## Prerequisites

- **Docker** (for running HA and njord together)
- **.NET 10 SDK** (for service development)
- **Python 3.12+** (for integration development)
- **Git**

## Clone both repos

```bash
git clone https://github.com/st0o0/njord.git
git clone https://github.com/st0o0/ha-njord.git
```

## Run the full stack

Create a `docker-compose.yml` (or use the one in ha-njord):

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
      - Njord__Locations__0__Name=home
      - Njord__Locations__0__Latitude=47.05
      - Njord__Locations__0__Longitude=8.31
      - Njord__Models__0=icon_eu
      - Njord__Models__1=ecmwf_ifs025
      - Njord__Enrichment__Consensus__Enabled=true
      - Njord__Enrichment__Alerts__Enabled=true
      - Njord__Enrichment__Derived__Enabled=true
      - Njord__Enrichment__Trends__Enabled=true
      - Njord__Enrichment__Indices__Enabled=true
      - Njord__Enrichment__History__Enabled=true

volumes:
  ha-config:
  njord-data:
```

### Building njord locally

To run a local build instead of a published image:

```bash
cd njord
docker build -t njord:local .
```

Then start the stack:

```bash
docker compose up -d
```

Home Assistant will be available at `http://localhost:8123`. The njord integration code is mounted read-only from your `ha-njord` checkout — changes to the Python code are reflected on HA restart.

## Running without Docker

### Service only

From the `njord/src/` directory:

```bash
dotnet run --project Njord/Njord.csproj
```

The service uses `appsettings.Development.json` automatically. gRPC is available on port 8081.

### Integration tests only

See the [Service](./service) and [Integration](./integration) pages for running tests in each repo.
