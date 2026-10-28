# Integration Development

The Home Assistant custom integration is a Python project that connects to the njord service via gRPC.

**Repository:** [st0o0/ha-njord](https://github.com/st0o0/ha-njord)

## Project structure

```
ha-njord/
├── custom_components/njord/     # Integration source code
│   ├── __init__.py              # Setup, config/options flow hooks
│   ├── grpc_client.py           # gRPC client (unary + streaming)
│   ├── coordinator.py           # DataUpdateCoordinator with streaming
│   ├── weather.py               # Weather platform (domain data)
│   ├── sensor.py                # Sensor platform (alerts, diagnostics)
│   ├── event.py                 # Event platform (alert state changes)
│   ├── binary_sensor.py         # Binary sensor platform (stream connectivity)
│   ├── button.py                # Button platform (trigger poll)
│   ├── calendar.py              # Calendar platform
│   ├── config_flow.py           # Config + options flow
│   ├── translations/            # UI translations
│   └── proto/                   # Generated protobuf stubs
├── tests/                       # pytest test suite
├── protos/                      # Proto source files (synced from njord)
├── brand/                       # HACS brand assets
├── pyproject.toml
└── Makefile                     # Proto generation
```

## Proto regen

The proto source of truth is `protos/njord/v2/` in the njord repo (extend-only: field numbers are never reused). After a change, copy the files to `ha-njord/protos/` and run (requires Docker):

```bash
make proto
```

The sync can also run automatically on release, see [sync-protos.yml](https://github.com/st0o0/ha-njord/blob/main/.github/workflows/sync-protos.yml).

## Tests

```bash
pip install grpcio "protobuf>=5.0,<7.0" pytest pytest-asyncio pytest-homeassistant-custom-component voluptuous
python -m pytest tests/ -v --tb=short
```

Lint with `ruff format --check .` and `ruff check .`.
