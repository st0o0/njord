## Context

ASP.NET configuration loads in order: `appsettings.json` → `appsettings.{Environment}.json` → env vars → command-line. `dotnet run` defaults to `ASPNETCORE_ENVIRONMENT=Development`; Docker defaults to `Production`. The current `appsettings.json` contains dev data that gets baked into the production image.

All Options classes already have correct code defaults — `appsettings.json` only needs to exist for logging configuration that differs from the framework defaults.

## Goals / Non-Goals

**Goals:**
- Docker image starts without crashing when no env vars are set (validator still rejects missing locations, but no unhandled exception).
- `dotnet run` gives a working dev experience out of the box.
- MQTT is opt-in (default disabled).
- Configuration layering follows ASP.NET conventions.

**Non-Goals:**
- Gitignoring `appsettings.Development.json` — committed for team consistency (Option B).
- Changing Options class structure.

## Decisions

### D1: `appsettings.json` contains only logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Akka": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

No `Njord:` section. Every option has a code default. Users configure via env vars, `data/njord-config.json`, or compose.

### D2: `appsettings.Development.json` carries dev overrides

Contains: Debug logging, personal locations, model list, `ForecastDays: 16`, all parameter groups, all enrichment enabled, `Mqtt.Enabled: false`. Loaded only when `ASPNETCORE_ENVIRONMENT=Development`.

### D3: `MqttOptions.Enabled` defaults to `false`

The code default changes from `true` to `false`. Production deployments set `Njord__Mqtt__Enabled=true` alongside `Njord__Mqtt__Host`. This prevents the "missing host" crash in Docker and makes the gRPC-only mode the zero-config default.

### D4: `docker-compose.example.yml` shows `Enabled=true` as required

```yaml
- Njord__Mqtt__Enabled=true        # required for HA integration
- Njord__Mqtt__Host=192.168.1.x
```

## Risks / Trade-offs

- **[Breaking change for existing Docker deployments]** → Users who currently rely on `Mqtt.Enabled` defaulting to `true` and only set `Host` will get no MQTT. Mitigated: `docker-compose.example.yml` documents the required `Enabled=true`. Anyone using the example already has env vars — they just add one line.
