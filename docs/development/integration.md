# Integration Development

The Home Assistant custom integration is a Python project that connects to the njord service via gRPC.

**Repository:** [st0o0/ha-njord](https://github.com/st0o0/ha-njord)

## Project structure

```
ha-njord/
├── custom_components/njord/
│   ├── __init__.py              # Setup: client, coordinators, platforms, trigger_poll service
│   ├── config_flow.py           # Config flow (host+port) + options flow (status poll interval)
│   ├── coordinator.py           # NjordStatusCoordinator (status polling)
│   ├── stream_coordinator.py    # NjordStreamCoordinator (entity stream via Subscribe)
│   ├── entities.py              # Generic entity classes per OutputKind (alert, score, condition, trend, forecast, observation)
│   ├── weather.py               # Weather entity from EntityStateUpdate WeatherValue
│   ├── entity_factory.py        # Manifest-driven entity creation per platform
│   ├── sensor.py                # Server diagnostic sensors + entity factory
│   ├── binary_sensor.py         # Entity factory (alert binary sensors)
│   ├── button.py                # Trigger poll button
│   ├── grpc_client.py           # NjordClient: async gRPC wrapper (GetManifest, Subscribe, GetStatus, TriggerPoll)
│   ├── models.py                # Frozen dataclasses (status, ManifestEntity, EntityState)
│   ├── condition_mapper.py      # WMO weather_code + is_day -> HA condition string
│   ├── helpers.py               # Device info, current sample picker
│   ├── diagnostics.py           # HA diagnostics download
│   ├── const.py                 # DOMAIN, PollPhase, config constants
│   ├── manifest.json            # HA integration metadata
│   ├── strings.json             # English UI strings
│   ├── translations/            # UI translations (en, de)
│   └── proto/                   # Generated protobuf/gRPC stubs
├── protos/njord/                # Proto source files (synced from njord)
├── tests/                       # pytest test suite
├── brand/                       # HACS brand assets (icon.png, logo.svg)
└── pyproject.toml               # uv-managed dependencies
```

## Proto regen

The proto source of truth is `protos/njord/njord.proto` in the njord repo (extend-only: field numbers are never reused). After a change, copy the files to `ha-njord/protos/` and regenerate stubs:

```bash
docker run --rm -v "$PWD:/work" -w /work python:3.12-slim \
  sh -c "pip install --quiet 'grpcio-tools>=1.70,<1.79' 'protobuf>=5.0,<6.0' && \
  python -m grpc_tools.protoc \
    -Iprotos \
    --python_out=custom_components/njord/proto \
    --grpc_python_out=custom_components/njord/proto \
    protos/njord/njord.proto"
```

A GitHub Actions workflow (`sync-protos.yml`) automates this on njord releases.

## Tests

```bash
# Docker (works on all platforms including Windows)
docker run --rm -v "$PWD:/work" -w /work -e UV_PROJECT_ENVIRONMENT=/opt/venv python:3.12-slim \
  sh -c "pip install --quiet uv && uv sync --locked --all-extras && uv run pytest tests/ -v"

# Native (Linux/macOS only)
uv sync --locked --all-extras
uv run pytest tests/ -v --tb=short
```

Lint: `uv run ruff format --check . && uv run ruff check .`
