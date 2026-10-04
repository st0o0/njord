# ha-njord (HA Integration)

Home Assistant custom integration for the njord weather service. Connects via gRPC, creates native `weather` entities with hourly+daily forecast support.

## Architecture

ha-njord is a **pure consumer** — no config logic, no write access to njord. It reads forecasts and config via gRPC and displays them as HA weather entities.

```
njord (gRPC server)  ──►  ha-njord (HA integration)
  GetLocations()              Config Flow (Host+Port)
  GetModels()                 weather.njord_{loc}_{model}
  GetForecast()               DataUpdateCoordinator (5min)
  GetConfig()                 WMO → HA condition mapping
```

## Tech Stack

- Python 3.12+, grpcio, protobuf
- Proto stubs are generated and committed (no build step for end users)

## Key Commands

Run from `ha/`:

```bash
# Generate proto stubs (Docker) — reads from repo-root protos/
make proto

# Run tests (Docker)
make test
```

## Project Structure

```
custom_components/njord/     # at repo root (HACS requirement)
ha/
├── tests/                   # pytest tests
├── Makefile                 # proto generation + Docker test runner
├── pyproject.toml           # Python project config
└── docs/                    # HA integration docs
protos/njord/v2/             # Shared proto source (single source of truth)
```

## Proto Management

Proto files live at `protos/njord/v2/` (repo root), shared with the .NET service.
Run `make proto` from `ha/` to regenerate Python stubs into `custom_components/njord/proto/`.

## Conventions

- Git: commit conventionally, NEVER `git push` — the user pushes
- Tests: `ha/tests/`, run via Docker or CI
- HA module stubs in `tests/conftest.py` allow testing without homeassistant installed
- Config flow and weather platform tests that need full HA are `@pytest.mark.skip`
