## Why

njord currently has no self-contained dev environment — the minimal `docker-compose.yml` requires an external Mosquitto broker (usually on a Home Assistant host) and shows only ~5 of 40+ configurable options. A developer or evaluator cannot spin up njord in isolation to see it work. Adding an Aspire AppHost with bundled Mosquitto and MQTT Explorer lets anyone run njord with a single F5, while a fully-commented docker-compose example serves as the configuration reference for production deployments.

## What Changes

- **Rename** `docker-compose.yml` → `docker-compose.example.yml` with every configurable option documented as commented environment variables, grouped by section.
- **Add Aspire 13 AppHost project** (`src/Njord.AppHost/`) that orchestrates:
  - Mosquitto broker (container) with a minimal `mosquitto.conf`.
  - MQTT Explorer UI (container) wired to the broker for visual debugging.
  - Optional PostgreSQL (container) toggled via launch profile; SQLite is the default.
  - njord itself as a project reference, with MQTT host auto-injected.
- **Two launch profiles** in the AppHost: `sqlite` (default) and `postgres`.
- **Add the AppHost project to `Njord.slnx`.**

## Non-goals

- **ServiceDefaults / Serilog / OpenTelemetry** — deferred to a separate "observability" change; the AppHost ships without a ServiceDefaults project for now.
- **Changes to njord application code** — this change only adds orchestration and documentation artifacts; no modifications to `src/Njord/`.
- **API budget impact** — no polling changes; request patterns are unchanged.

## Capabilities

### New Capabilities
- `aspire-apphost`: Aspire 13 AppHost project orchestrating njord + Mosquitto + MQTT Explorer + optional PostgreSQL for local development.
- `docker-compose-reference`: Fully-commented docker-compose.example.yml documenting all configurable options for production deployment.

### Modified Capabilities

(none — no existing spec-level requirements change)

## Impact

- **New project**: `src/Njord.AppHost/` (Aspire AppHost, not deployed to production).
- **New files**: `mosquitto.conf` (broker config for dev), launch profiles.
- **Renamed file**: `docker-compose.yml` → `docker-compose.example.yml`.
- **Solution**: `Njord.slnx` gains the AppHost project.
- **Dependencies**: Aspire 13 SDK + `Aspire.Hosting.PostgreSQL` NuGet package (AppHost-only, not added to the njord service itself).
- **No breaking changes** to the njord service, its configuration surface, or its Docker image.
