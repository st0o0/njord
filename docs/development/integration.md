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
│   ├── weather.py               # Weather platform (per-model + consensus)
│   ├── sensor.py                # Sensor platform (alerts, indices, derived, ...)
│   ├── event.py                 # Event platform (alert state changes)
│   ├── binary_sensor.py         # Binary sensor platform (inversion, streams)
│   ├── button.py                # Button platform (trigger poll)
│   ├── config_flow.py           # Config + options flow
│   └── proto/                   # Generated protobuf stubs
├── tests/                       # pytest test suite
├── protos/                      # Proto source files (synced from njord)
├── brand/                       # HACS brand assets
├── pyproject.toml
└── Makefile                     # Proto generation
```

## Run tests

```bash
pip install grpcio "protobuf>=5.0,<7.0" pytest pytest-asyncio pytest-homeassistant-custom-component voluptuous
python -m pytest tests/ -v --tb=short
```

## Lint

```bash
pip install ruff
ruff format --check .
ruff check .
```

## Proto management

Proto source files live in `protos/` (synced from the njord repo). To regenerate Python stubs after a proto change:

```bash
make proto
```

This requires Docker (uses the `grpc/python` image for deterministic codegen). See [Proto Management](./protos) for the sync workflow.
