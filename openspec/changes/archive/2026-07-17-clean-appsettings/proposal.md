## Why

`appsettings.json` bakes dev-specific data (personal locations, model lists, enrichment overrides, Debug logging) into the Docker image. A fresh `docker run` without env vars either crashes (missing MQTT host) or runs with the developer's home coordinates. The configuration layer should follow the ASP.NET convention: code defaults in Options classes, `appsettings.json` as minimal production baseline, `appsettings.Development.json` for dev comfort.

## What Changes

- **Strip `appsettings.json`** down to production logging only — no `Njord:` section. All defaults already live in the Options classes.
- **Create `appsettings.Development.json`** with dev-specific overrides: locations, models, `ForecastDays: 16`, all enrichment features enabled, `Mqtt:Enabled: false`, Debug logging. Committed to repo (Option B).
- **Change `MqttOptions.Enabled` default from `true` to `false`** in code. MQTT is opt-in: users enable it explicitly when they have a broker. Docker-compose and env vars set `Enabled=true` + `Host=...`.
- **Update `docker-compose.example.yml`** to reflect `Enabled=true` as explicit opt-in.
- **Update `appsettings.Example.json`** to match the new defaults.
- **Update `CLAUDE.md`** to document the configuration layering.

## Non-goals

- Restructuring the Options classes themselves — they already follow Microsoft conventions with proper code defaults.
- Changing `data/njord-config.json` runtime override mechanism — it stays as-is.
- No API budget impact — this change does not alter polling behavior.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `service-configuration`: `MqttOptions.Enabled` default changes to `false`; `appsettings.json` stripped to production-only; `appsettings.Development.json` introduced for dev overrides.

## Impact

- **Docker image**: starts cleanly without any `Njord:` env vars (no locations = validator fails gracefully with a clear message, no MQTT crash).
- **Dev workflow**: `dotnet run` loads `appsettings.Development.json` automatically (ASP.NET default `ASPNETCORE_ENVIRONMENT=Development`), pre-configured with locations and MQTT disabled.
- **Existing deployments**: docker-compose users already set `Njord__Mqtt__Host` — they need to add `Njord__Mqtt__Enabled=true` (or it's already in their compose from the example).
