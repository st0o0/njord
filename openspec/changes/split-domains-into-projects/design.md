## Context

After unify-alert-types, all alerts share one type system. Now the domain-specific code can be cleanly split.

## Goals / Non-Goals

**Goals:** FunkArr-style one-project-per-domain isolation.
**Non-Goals:** New functionality.

## Decisions

### D1: Domain projects reference only Njord.Core (and below)
Same reference direction as before. Domain projects never reference each other. They connect through Njord.Messages and Njord.Core interfaces (IProjectionGroup, IAlertRule).

### D2: OpenMeteo mappings split by owner
`OpenMeteoAirQualityMappings` moves to `Njord.AirQuality/Mapping/`. `OpenMeteoFloodMappings` to `Njord.Flood/Mapping/`. The shared `OpenMeteoClient` and `OpenMeteoGateway` stay in `Njord.OpenMeteo`.

### D3: MQTT payloads move to domain projects
`WeatherPayload` → `Njord.Weather.Alerts/Payloads/` (or stays in Mqtt since Weather is the "base" domain). `AirQualityPayload` → `Njord.AirQuality/Payloads/`. `FloodPayload` → `Njord.Flood/Payloads/`.

### D4: Each domain has AddXxx() DI extension
`Njord.Weather.Alerts` → `AddWeatherAlerts(this IServiceCollection)`. `Njord.AirQuality` → `AddAirQuality(this IServiceCollection)`. `Njord.Flood` → `AddFlood(this IServiceCollection)`. Host calls all three.

### D5: Setup containers in host
```csharp
builder.WithSetup<WeatherAlertSetupContainer>()
       .WithSetup<AirQualitySetupContainer>()
       .WithSetup<FloodSetupContainer>()
```
Each ~15 lines. Replaces the monolithic `AnalysisSetupContainer`.
