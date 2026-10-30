## REMOVED Requirements

### Requirement: Satellite is a fetched domain
**Reason**: Satellite archive data provides marginal value over forecast GTI. The 3h staleness cutoff means observations are frequently discarded. Costs ~720 calls/month/location for a feature that is off by default.
**Migration**: SolarYield uses forecast GTI directly. SatelliteRadiation feature is removed. Users relying on `Njord:Satellite:Tilt`/`Azimuth` migrate to `Njord:Solar:Tilt`/`Azimuth`.

### Requirement: SatelliteRadiationPoint carries all irradiance components
**Reason**: No consumer remains after removing the Satellite domain.
**Migration**: None needed; types are internal.

### Requirement: Satellite is polled only when needed
**Reason**: Domain no longer exists.
**Migration**: None needed.

### Requirement: Satellite options
**Reason**: `PastHours` is removed with the domain. `Tilt` and `Azimuth` move to `SolarOptions` under `Njord:Solar`.
**Migration**: Change config from `Njord:Satellite:Tilt`/`Azimuth` to `Njord:Solar:Tilt`/`Azimuth`. Remove `Njord:Satellite:PastHours`.

### Requirement: Satellite and ensemble irradiance are blended
**Reason**: Without satellite data the blend always degrades to forecast-only, making `SolarIrradianceBlend` a no-op wrapper.
**Migration**: SolarYield uses forecast GTI directly.

### Requirement: SatelliteRadiation feature reports observed irradiance
**Reason**: Niche feature; observed irradiance is not actionable for HA automations.
**Migration**: Users disable the feature (it was off by default).

### Requirement: Satellite domain on the wire
**Reason**: No data to serve.
**Migration**: ha-njord removes satellite entity handling. Proto drops `DOMAIN_SATELLITE` and `SatelliteSample`.

### Requirement: Satellite options carry panel orientation
**Reason**: Replaced by `SolarOptions` in the `solar-options` capability.
**Migration**: `Njord:Satellite:Tilt`/`Azimuth` becomes `Njord:Solar:Tilt`/`Azimuth`.

### Requirement: Forecast and Ensemble clients use the satellite orientation
**Reason**: Replaced by equivalent requirement in `solar-options` using `SolarOptions` instead of `SatelliteOptions`.
**Migration**: Clients change constructor parameter from `IOptions<SatelliteOptions>` to `IOptions<SolarOptions>`.
