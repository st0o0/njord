## Context

Locations may omit coordinates; `LocationResolutionService` (hosted, before the actor system) geocodes them and fails startup on no match or HTTP error. `ClimateBaselineService` (hosted) fills an `IClimateBaselineStore` that only `ClimateAnomalyHandler` reads. Both live in `Njord.OpenMeteo`/`Njord.Core` and are wired in `OpenMeteoSetupContainer` and `Njord.Weather.ServiceCollectionExtensions`.

## Goals / Non-Goals

- Goals: delete both APIs and everything only they use; make coordinates mandatory with clear validation errors; keep wire vocabularies consistent.
- Non-Goals: a replacement anomaly source; changes to `seasonal-outlook`; polling or budget logic changes.

## Decisions

- **Coordinates required, validated at startup.** `NjordOptionsValidator` reports per-location errors for missing Latitude/Longitude and out-of-range values (-90..90, -180..180). `HasCoordinates` is removed. Alternative: keep geocoding as opt-in; rejected, the fragility and code remain.
- **Delete ClimateAnomaly end to end**, including the `LifestyleKind` member, `LifestyleMappings` switch arms, the `ClimateAnomalyComputed` event and the toggle. Per project rules (no preemptive legacy code) no tombstone is kept; 0.x permits the breaking change. Mapping specs still pin every remaining member.
- **Obsolete config key.** An existing `Njord__Enrichments__ClimateAnomaly` setting is simply unbound; no error. Called out via the `feat!` commit.
- **Slow-domain spec removed.** Climate was its only consumer; `seasonal-outlook` reintroduces it if needed.

## Risks / Trade-offs

- Name-only locations no longer start -> validator message names the location and missing fields; Config Builder always emits coordinates.
- `seasonal-outlook` depends on `IClimateBaselineStore` -> must be revised before it is applied.
- Hidden references (docs, e2e, ha-njord entities) -> grep for `ClimateAnomaly`/`geocod` in the validation chunk; check ha-njord for a climate_anomaly lifestyle entity.

## Migration Plan

Operators add `Latitude`/`Longitude` to each location and drop the `ClimateAnomaly` toggle. Rollback is a git revert; no persistence records are touched.

## Open Questions

- Does ha-njord reference the `climate_anomaly` lifestyle kind and need a matching change in the same release?
