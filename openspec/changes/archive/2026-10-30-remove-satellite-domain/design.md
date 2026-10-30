## Context

njord fetches data from 7 Open-Meteo domains. Satellite is the only archive-based domain (not a forecast model). It provides observed solar irradiance used by two features: SolarYield (optional dependency, degrades to forecast-only) and SatelliteRadiation (hard dependency, off by default). The blend mechanism (`SolarIrradianceBlend`) that merges satellite and ensemble GTI discards satellite data older than 3 hours, which happens frequently due to archive processing latency.

## Goals / Non-Goals

**Goals**: Remove Satellite cleanly while preserving SolarYield functionality and panel orientation config.

**Non-goals**: Changing SolarYield behavior beyond removing the blend. Touching any other domain.

## Decisions

### D1: Rename SatelliteOptions to SolarOptions under Njord:Solar

ForecastClient and EnsembleClient need `Tilt` and `Azimuth` for GTI URL parameters. These currently live in `SatelliteOptions` (`Njord:Satellite`). Rename to `SolarOptions` (`Njord:Solar`) and keep only Tilt/Azimuth, dropping PastHours.

**Alternative**: Keep the name `SatelliteOptions` -- rejected because the class no longer has anything to do with the satellite domain.

### D2: SolarOptions stays in Njord.OpenMeteo

Only the OpenMeteo clients (Forecast, Ensemble) consume Tilt/Azimuth. No reason to move it to Core.

### D3: SolarYield uses forecast GTI directly

Remove `SolarIrradianceBlend`. SolarYield's `Project` method reads forecast `GlobalTiltedIrradiance` hours directly. The feature already produced identical output when satellite data was absent or stale.

### D4: Remove DomainKind.Satellite as a breaking change

While 0.x, breaking changes are fine. Remove the enum member and fix the exhaustive switches. Update persistence records (add a comment that the value 6 was Satellite, for deserialization safety of old journals -- but since Satellite was off by default, old journals with Satellite events are unlikely).

### D5: Proto removal

Remove `DOMAIN_SATELLITE = 7` and `SatelliteSample` from `weather.proto`. Reserve the enum value with a comment. ha-njord must be updated in parallel.

## Risks / Trade-offs

- [Config breaking change] Users with `Njord:Satellite:Tilt`/`Azimuth` must migrate to `Njord:Solar:Tilt`/`Azimuth` -- mitigated by documenting in release notes and being 0.x.
- [Persistence journal entries] Old journals may contain `PersistedDomainKind.Satellite` events -- mitigated by keeping the numeric value reserved in `PersistedDomainKind` with a comment.
- [ha-njord breakage] Must co-release or ha-njord will fail on missing satellite entities -- mitigated by coordinating both repos.

## Migration Plan

1. Implement removal in njord (this change).
2. Update ha-njord to remove satellite entity handling.
3. Ship both together (already the release pattern per memory).
4. Document config migration in release notes.

## Open Questions

None.
