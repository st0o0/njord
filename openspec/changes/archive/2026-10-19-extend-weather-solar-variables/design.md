## Context

Solar radiation variables are part of the standard Open-Meteo forecast/ensemble API — no separate endpoint. Adding these variables to the existing Ensemble request provides solar exposure data at zero additional API cost.

## Goals / Non-Goals

**Goals:** Solar variables in Ensemble request, WeatherPoint carries them, optional SolarYield enrichment.
**Non-Goals:** Separate solar endpoint, satellite observation data, detailed PV simulation.

## Decisions

### D1: Extend existing Ensemble request

Add `shortwave_radiation`, `direct_radiation`, `diffuse_radiation`, `direct_normal_irradiance`, `sunshine_duration` to the hourly variable list. No additional API call.

### D2: WeatherPoint extended

WeatherPoint gains solar fields (may already have some from core-domain-model). All optional (double?).

### D3: Optional SolarYield enrichment

Simple daily PV yield estimate from direct + diffuse radiation. Uses a configurable panel spec (azimuth, tilt, kWp) if provided, otherwise raw radiation data only.
