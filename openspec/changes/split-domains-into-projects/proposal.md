## Why

FunkArr's core pattern: one feature = one project. Njord.Alerts currently mixes 3 domains (Weather 14 rules, AQ 2 rules, Flood 2 rules+2 projectors) with 2 incompatible architectures. Njord.Mqtt has 47 files including 16 domain-specific payloads. Each domain should be self-contained: its own fetch mappings, alerts, payloads, DI registration, and test project.

## What Changes

- Create `Njord.Weather.Alerts/` — 14 weather alert rules, WeatherAlertProjector, WeatherAlertEnrichment, AlertAnalyzer (pure logic), weather alert DI registration
- Create `Njord.AirQuality/` — AQ-specific OpenMeteo mappings, AQ alert rules (Pollen, Pm25), AQ projector, AQ MQTT payloads, AQ DI registration
- Create `Njord.Flood/` — Flood-specific OpenMeteo mappings, Flood alert rules (FloodRisk, FloodRain), Flood projectors, Flood MQTT payloads, Flood DI registration
- Create test projects: `Njord.Weather.Alerts.Tests/`, `Njord.AirQuality.Tests/`, `Njord.Flood.Tests/`
- Njord.Alerts becomes empty and is deleted (or renamed to Njord.Weather.Alerts)
- Njord.Mqtt loses domain-specific payloads (they move to domain projects)
- Njord.OpenMeteo keeps the shared client and gateway; domain-specific mapping files move to domain projects
- Update architecture tests for new project structure

## Non-goals

- Changing alert logic or thresholds.
- Adding new domains.

## Capabilities

### New Capabilities
- `domain-project-isolation`: Each domain is a self-contained project

### Modified Capabilities
- `architecture-zone-enforcement`: Updated for new project structure

## Impact

- **Solution**: +3 projects, +3 test projects, -1 (Njord.Alerts)
- **Njord.Mqtt**: ~16 fewer files (payloads move to domains)
- **Njord.OpenMeteo**: Domain mappings move out, shared client/gateway stays
- **Architecture tests**: Rewritten for new zones
