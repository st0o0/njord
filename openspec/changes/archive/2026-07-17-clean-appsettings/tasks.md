## 1. MQTT Default Change

- [x] 1.1 Change `MqttOptions.Enabled` default from `true` to `false` in `src/Njord/Configuration/MqttOptions.cs`
- [x] 1.2 Update the `Mqtt_is_enabled_by_default` test in `src/Njord.Tests/Configuration/NjordOptionsValidatorSpec.cs` to assert `false`
- [x] 1.3 Update the `Missing_mqtt_host_is_rejected` test — it needs `Enabled = true` explicitly now since the default is `false`

## 2. Appsettings Split

- [x] 2.1 Strip `src/Njord/appsettings.json` to production-only logging — remove the entire `Njord:` section
- [x] 2.2 Create `src/Njord/appsettings.Development.json` with dev overrides: Debug logging, locations (borken, winterswijk), models, `ForecastDays: 16`, `Parameters.Groups: [Weather, Solar, Soil]`, all enrichment enabled, `Mqtt.Enabled: false`

## 3. Documentation Updates

- [x] 3.1 Update `src/Njord/appsettings.Example.json` — document `Mqtt.Enabled` default as `false`, remove any assumption that MQTT is on by default
- [x] 3.2 Update `docker-compose.example.yml` — make `Njord__Mqtt__Enabled=true` an uncommented required line alongside Host
- [x] 3.3 Update `CLAUDE.md` — document the configuration layering (appsettings.json = production, Development.json = dev, env vars = Docker)

## 4. Validation

- [x] 4.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 4.2 Run unit tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 4.3 Run slopwatch: `dotnet slopwatch` from repo root
