# Local development

## Docker stack

Build a local image and start njord with Home Assistant:

```bash
docker build -t njord:local .
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
      - ../ha-njord/custom_components/njord:/config/custom_components/njord:ro
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
      - Njord__Features__FloodRisk__Enabled=true

volumes:
  ha-config:
  njord-data:
```

`docker compose up -d`; Home Assistant at `http://localhost:8123`. Python changes in ha-njord show up after an HA restart.

## Debugging

Run the service directly with `dotnet run --project src/Njord/Njord.csproj`. The Development environment loads `appsettings.Development.json`. Override log levels:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Njord.Polling": "Debug",
        "Njord.Pipeline": "Debug"
      }
    }
  }
}
```

## ha-njord tests

ha-njord uses `pytest-homeassistant-custom-component`. Native pytest fails on Windows (event-loop fixtures assume Unix). Run via Docker:

```bash
cd ha-njord
docker run --rm -v "$PWD:/work" -w /work -e UV_PROJECT_ENVIRONMENT=/opt/venv python:3.12-slim \
  sh -c "pip install --quiet uv && uv sync --locked --all-extras && uv run pytest tests/ -v"
```

Lint: `uv run ruff format --check . && uv run ruff check .`

## Mutation testing

[Stryker.NET](https://stryker-mutator.io/) changes production code and re-runs tests; a mutant that no test kills survived and points at a weak assertion.

```bash
bash scripts/run-mutation.sh                        # all targets (Docker)
bash scripts/run-mutation.sh Njord.Features         # single target
bash scripts/run-mutation.sh Njord.Features --local # direct (Linux/macOS)
```

Targets are listed in `src/mutation-targets.txt`. Reports go to `src/<Project>.Tests/StrykerOutput/` (git-ignored). Thresholds: target 80%, warning 60%, break 60%.

Not mutated: `*Options.cs`, `*OptionsValidator.cs`, `ServiceCollectionExtensions.cs`, `*SetupContainer.cs`, string mutations.

## Full-stack E2E

The `e2e-test` skill (`.claude/skills/e2e-test/SKILL.md`) runs njord + ha-njord + Home Assistant in Docker Compose and validates the complete flow: gRPC API, 6 domains, 30 alert types, 23 observations, entity validation, health endpoints, multi-cycle budget tracking.

## OpenSpec validation

```bash
openspec validate --all --no-interactive
bash scripts/check-openspec-root.sh
```
