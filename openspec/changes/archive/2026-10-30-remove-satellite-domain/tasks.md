## 1. Introduce SolarOptions (replacement for Tilt/Azimuth)

- [x] 1.1 Create `SolarOptions` and `SolarOptionsValidator` in `Njord.OpenMeteo/Configuration/` with `Njord:Solar` section, `Tilt` (30, 0..90), `Azimuth` (180, 0..360)
- [x] 1.2 Register `SolarOptions` with `AddNjordOptions<SolarOptions, SolarOptionsValidator>()` in `OpenMeteoSetupContainer`
- [x] 1.3 Update `ForecastClient` and `EnsembleClient` constructors: `IOptions<SatelliteOptions>` to `IOptions<SolarOptions>`
- [x] 1.4 Write `SolarOptionsValidatorTests` (port from `SatelliteOptionsValidatorTests`, drop PastHours tests)
- [x] 1.5 Update `OpenMeteoClientTests` to use `SolarOptions` instead of `SatelliteOptions`

## 2. Remove Satellite domain types from Core

- [x] 2.1 Remove `DomainKind.Satellite` enum member and fix all exhaustive switches
- [x] 2.2 Remove `Njord.Core/Solar/` directory (`SatelliteRadiationPoint`, `SatelliteParams`, related types)
- [x] 2.3 Remove satellite entries from `DomainRequirements` and `DomainRequirementsExtensions`
- [x] 2.4 Fix `DomainKind` tests and any Core tests referencing Satellite

## 3. Remove Satellite from OpenMeteo

- [x] 3.1 Remove `Njord.OpenMeteo/Satellite/` directory (client, response DTO, mapping, API enum)
- [x] 3.2 Remove `SatelliteOptions`, `SatelliteOptionsValidator` (replaced by SolarOptions in step 1)
- [x] 3.3 Remove satellite dispatch from `OpenMeteoGateway` and endpoint registration
- [x] 3.4 Remove satellite from `DomainCallConfig` and `ConfigureDomainCallConfig`
- [x] 3.5 Remove `Njord.OpenMeteo.Tests/SatelliteRadiationTests.cs` and satellite validator tests

## 4. Remove Satellite from Budget and Polling

- [x] 4.1 Remove satellite projection from `BudgetGuard`
- [x] 4.2 Remove satellite references from PollWorker domain handling (if any domain-specific logic exists)
- [x] 4.3 Fix any budget or polling tests referencing Satellite

## 5. Remove SatelliteRadiation feature and SolarIrradianceBlend

- [x] 5.1 Remove `SatelliteRadiation` feature from `Njord.Features/` (rule, config, descriptor)
- [x] 5.2 Remove `SolarIrradianceBlend` from `Njord.Features/`
- [x] 5.3 Simplify `SolarYield` to use forecast GTI directly (remove blend call, remove optional satellite requirement from descriptor)
- [x] 5.4 Remove `SatelliteRadiation` registration from `AddFeatures`
- [x] 5.5 Remove satellite feature tests and update SolarYield tests

## 6. Remove Satellite from gRPC and proto

- [x] 6.1 Remove `DOMAIN_SATELLITE` from proto enum (reserve the value with a comment)
- [x] 6.2 Remove `SatelliteSample` and satellite oneof case from `weather.proto`
- [x] 6.3 Remove satellite mapping in `Njord.Grpc/Mapping/`
- [x] 6.4 Fix gRPC mapping tests

## 7. Remove Satellite from Persistence

- [x] 7.1 Reserve `Satellite = 6` in `PersistedDomainKind` with a comment (do not remove the numeric value)
- [x] 7.2 Remove satellite-specific persistence records if any exist beyond the shared `DomainPolled`
- [x] 7.3 Fix wire format snapshot tests

## 8. Update configuration and docs

- [x] 8.1 Remove `Njord:Satellite` section from `appsettings.Example.json`, add `Njord:Solar` with Tilt/Azimuth
- [x] 8.2 Remove `SatelliteRadiation` entry from `docs/data/features.json`
- [x] 8.3 Update Config Builder (`budget.ts`, `types.ts`, `serializer.ts`) to remove satellite domain and add solar options
- [x] 8.4 Update `docker-compose.example.yml` to remove satellite env vars, add solar
- [x] 8.5 Update AGENTS.md and CLAUDE.md to remove Satellite references

## 9. Verify

- [x] 9.1 `dotnet build Njord.slnx`
- [x] 9.2 `dotnet format Njord.slnx --verify-no-changes`
- [x] 9.3 Run all test projects
- [x] 9.4 `openspec validate --change remove-satellite-domain --no-interactive`
