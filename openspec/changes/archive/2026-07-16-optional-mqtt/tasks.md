## 1. Config Flag & Validation

- [x] 1.1 Add `Enabled` property (bool, default `true`) to `src/Njord/Configuration/MqttOptions.cs`
- [x] 1.2 Update `NjordOptionsValidator.Validate` in `src/Njord/Configuration/NjordOptionsValidator.cs` to skip `Mqtt.Host` validation when `Mqtt.Enabled` is `false`
- [x] 1.3 Add `"Enabled": true` to `src/Njord/appsettings.json` and `src/Njord/appsettings.Example.json` in the `Mqtt` section
- [x] 1.4 Write `NjordOptionsValidatorMqttDisabledSpec` in `src/Njord.Tests/Configuration/` — test that missing `Mqtt.Host` passes validation when `Enabled = false` and fails when `Enabled = true`

## 2. Conditional DI Registration

- [x] 2.1 In `NjordServiceSetup.SetupServices` (`src/Njord/Configuration/NjordServiceSetup.cs`): read `Mqtt:Enabled` from `IConfiguration`, guard registration of `MqttEgressTuning`, `MqttNetPublisher`, `IMqttConnection`, `IMqttTransport` behind the flag
- [x] 2.2 Guard `MqttConnectionHealthCheck` registration behind the same flag in `NjordServiceSetup`

## 3. Conditional Actor Registration

- [x] 3.1 In `NjordActorSystemSetup.BuildSystem` (`src/Njord/Configuration/NjordActorSystemSetup.cs`): guard `MqttConnectionActor`, `MqttEgressActor`, and `DiscoveryActor` registration behind `njordOptions.Mqtt.Enabled`

## 4. ModelStateActor Guard

- [x] 4.1 In `ModelStateActor` (`src/Njord/Egress/ModelStateActor.cs`): inject `NjordOptions`, make `DiscoveryActor` resolution conditional on `Mqtt.Enabled`; skip `ModelCapabilityLearned` sends when `_discoveryActor` is null
- [x] 4.2 Write `ModelStateActorMqttDisabledSpec` in `src/Njord.Tests/Egress/` — test that `ModelStateActor` starts and processes forecasts when MQTT is disabled (no `DiscoveryActor` registered)

## 5. Validation

- [x] 5.1 Run full unit test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 5.2 Run integration tests: `dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj` from `src/`
- [x] 5.3 Run slopwatch: `dotnet slopwatch` from repo root
