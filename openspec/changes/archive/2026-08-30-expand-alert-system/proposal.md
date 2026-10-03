## Why

The alert system currently has two gaps: four alert types (Frost, Storm, Fog, PressureDrop) are capped at Yellow severity regardless of how extreme conditions get, and several safety-relevant weather phenomena (ice/black ice, wind chill, poor visibility, tropical nights, oppressive humidity) have no alerts at all despite all required API parameters already being available in the ParameterRegistry. Expanding the system closes both gaps, making njord a more complete warning source for Home Assistant users.

## What Changes

### Severity extensions for existing alerts
- **Frost**: extend from None/Yellow to None/Yellow/Orange/Red with tiered thresholds (default [0, -5, -15] C). **BREAKING**: `FrostThreshold` (double) becomes `FrostThresholds` (double[]).
- **Storm**: extend from None/Yellow to None/Yellow/Orange/Red with tiered thresholds (default [17, 25, 33] m/s). **BREAKING**: `StormGustThreshold` (double) becomes `StormGustThresholds` (double[]).
- **Fog**: extend from None/Yellow to None/Yellow/Orange based on duration (persistent fog >= 4h).
- **PressureDrop**: extend from None/Yellow to None/Yellow/Orange for severe drops (>= 10 hPa in 3h).

### New alert types
- **Ice**: rain falling at near-freezing temperatures (temp <= 2 C + precipitation as rain), with severity escalation when soil temperature confirms frozen ground. Severity: Yellow/Orange/Red.
- **WindChill**: extreme apparent temperature driven by wind (apparent_temp <= -10/-20/-30 C). Severity: Yellow/Orange/Red.
- **Visibility**: direct visibility parameter below safety thresholds (< 1000m / 200m / 50m). Severity: Yellow/Orange/Red.
- **TropicalNight**: overnight minimum temperature stays above 20 C, preventing recovery from daytime heat. Severity: Yellow/Orange/Red (20/23/25 C).
- **Humidity**: high dewpoint indicating oppressive mugginess independent of air temperature (dewpoint >= 16/21/24 C). Severity: Yellow/Orange/Red.

## Non-goals

- No new API parameters or polling changes. All data is already requested. Zero API-budget impact.
- No changes to the enrichment pipeline structure, MQTT topology, or discovery device model. New alerts are additional components on the existing per-location alerts device.
- No changes to the Index system (indices score "how good for X", alerts warn "danger from X").

## Capabilities

### New Capabilities

- `new-alert-types`: Five new AlertType enum values (Ice, WindChill, Visibility, TropicalNight, Humidity) with evaluator functions, AlertOptions configuration, and discovery components.
- `severity-extensions`: Tiered severity for Frost, Storm, Fog, PressureDrop with new/changed AlertOptions fields.

### Modified Capabilities

- `threshold-alerts`: AlertType enum grows from 9 to 14 values. AlertResult always contains 14 alerts. Discovery payload grows from 9 to 14 binary_sensor components. AlertOptions gains new fields and two breaking renames.

## Impact

- **Domain**: `AlertType` enum, `AlertEvaluator`, `AlertTypeExtensions`
- **Configuration**: `AlertOptions` (new fields + two breaking renames), config validation
- **Egress**: `DiscoveryPayloadBuilder` (14 components), `StatePayloadBuilder` (14 alert entries)
- **Persistence**: `EnrichmentSnapshotDtos` must handle new alert types on recovery (extend-only: new types deserialize as new entries, old snapshots without them produce Alert.None)
- **Tests**: new evaluator specs, updated discovery/state payload specs, updated serialization specs
- **gRPC**: `EnrichmentProtoMapper` maps new alert types
