# satellite-radiation Specification

## Purpose

Removed. The Satellite domain provided observed solar irradiance from satellite-api.open-meteo.com/v1/archive. It was removed because the 3h staleness cutoff meant data was frequently discarded, SolarYield worked without it (optional dependency), and SatelliteRadiation was a niche feature most HA users would not act on. Costs 720 calls/month/location for marginal value. Panel orientation (Tilt/Azimuth) moved to `solar-options`.

## Requirements

### Requirement: Capability removed

All requirements of this capability have been removed. The system SHALL NOT contain `DomainKind.Satellite`, `SatelliteRadiationPoint`, `SatelliteParams`, `SatelliteRadiationClient`, `SolarIrradianceBlend`, or `SatelliteRadiation` feature types.

#### Scenario: No satellite types exist
- **WHEN** the system is compiled
- **THEN** no type referencing Satellite as a domain SHALL exist
