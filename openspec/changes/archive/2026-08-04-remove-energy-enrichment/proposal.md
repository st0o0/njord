# Remove Energy Enrichment

## Problem

The energy enrichment feature (HeatingDemand, CopEstimate, CopOptimalHours, ShadingScore, BatteryStrategy, NightCoolingPotential) is being removed from njord. Energy management will be handled outside of njord in the future.

## Proposal

Completely remove the energy enrichment feature from the codebase, including:

- Domain logic (`EnergyForecaster`, `EnergyResult`, `CopOptimalEntry`)
- Enrichment implementation (`EnergyEnrichment`)
- Configuration (`EnergyOptions`, `EnergyOptionsValidator`)
- DI registration
- gRPC surface (proto messages, mapper, admin mutations, weather service)
- MQTT egress (discovery payloads, state payloads)
- Persistence DTO mapping
- All associated tests
- Documentation and OpenSpec specs

## Scope

This is a purely destructive change. No new code is introduced.

### Delete entirely (7 files)

| File | Content |
|---|---|
| `src/Njord/Domain/Analysis/EnergyForecaster.cs` | Static computation methods |
| `src/Njord/Domain/Analysis/EnergyResult.cs` | Result record + Compute factory |
| `src/Njord/Enrichment/Features/EnergyEnrichment.cs` | IStatelessEnrichment impl |
| `src/Njord.Tests/Domain/Analysis/EnergyForecasterSpec.cs` | Unit tests |
| `src/Njord.Tests/Domain/Analysis/EnergyResultSpec.cs` | Unit tests |
| `src/Njord.Tests/Enrichment/Features/EnergyEnrichmentSpec.cs` | Integration tests |
| `openspec/specs/energy-management/spec.md` | OpenSpec specification |

### Edit to remove energy references (~50 files)

**Source (11 files):**
- `Configuration/EnrichmentOptions.cs` — remove `EnergyOptions` class + property
- `Configuration/EnrichmentOptionsValidation.cs` — remove `EnergyOptionsValidator`
- `Configuration/NjordServiceSetup.cs` — remove DI registrations
- `Grpc/EnrichmentProtoMapper.cs` — remove `MapEnergy` method + switch arm
- `Grpc/AdminGrpcService.cs` — remove energy config mutation block + response mapping
- `Grpc/WeatherGrpcService.cs` — remove energy case from enrichment event dispatch
- `Grpc/OpsGrpcService.cs` — remove energy from active enrichments
- `Mqtt/StatePayloadBuilder.cs` — remove `FromEnergy` method
- `Mqtt/DiscoveryPayloadBuilder.cs` — remove `BuildEnergy` method
- `Persistence/EnrichmentSnapshotDtos.cs` — remove EnergyResult type mapping

**Proto (3 files):**
- `protos/njord/v2/common.proto` — remove `EnergyUpdate`, `CopOptimalHour` messages
- `protos/njord/v2/admin.proto` — remove `EnergyConfig` message + fields in request/response
- `protos/njord/v2/weather.proto` — remove energy fields from enrichment messages

**Tests (9 files):**
- Remove energy-specific test methods and verified snapshots from shared test files

**Configuration (2 files):**
- `appsettings.Example.json` — remove Energy section
- `appsettings.Development.json` — remove Energy enabled override

**Documentation (4+ files):**
- `docs/configuration/enrichment.md`, `docs/data/enrichment.json`, `docs/index.md`, `docs/architecture.md`, `docs/mqtt-reference.md`

**OpenSpec specs (~20 files):**
- Remove energy from enrichment feature lists, examples, and cross-references in shared specs

**Root docs:**
- `CLAUDE.md` — remove energy from enrichment feature list and decisions
- `README.md` — remove energy feature mentions

## Breaking changes

- gRPC: `EnergyUpdate`, `EnergyConfig`, `CopOptimalHour` proto messages removed
- gRPC: `SetEnrichment` no longer accepts `energy` field
- gRPC: `GetEnrichments` / `StreamEnrichments` no longer emit energy events
- MQTT: energy device and entities no longer published
- Config: `Njord:Enrichment:Energy` section ignored

## Non-goals

- No replacement feature is introduced in this change
- CHANGELOG entries for historical energy releases are left untouched
- No migration/tombstone code — njord is unreleased, no backwards compatibility needed
