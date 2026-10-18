# alert-threshold-values Specification

## Purpose

Shared alert threshold records that eliminate field duplication across the config, update, and effective-config layers. A single `AlertThresholdValues` record carries the 16 threshold fields; `AlertSection` and `AlertConfigUpdate` wrap it instead of repeating the fields.

## Requirements

### Requirement: Shared threshold value record

The system SHALL provide an `AlertThresholdValues` record in `Njord.Messages/Alerts/` that carries the 16 alert threshold fields in their config-layer representation: `IReadOnlyList<double>` for multi-tier thresholds (Frost, Heat, StormGust, WindChill, Visibility, TropicalNight, Humidity) and scalar `double`/`int` for single-value thresholds (HeavyRainHourly, HeavyRainDaily, PressureDrop, PressureDropSevere, FogPersistentHours, Cape, ThunderstormPrecip, ThunderstormGust, Ice).

#### Scenario: AlertThresholdValues carries all 16 threshold fields
- **WHEN** an `AlertThresholdValues` instance is created
- **THEN** it SHALL expose exactly the 16 threshold fields with the same names as the current `AlertSection` (minus `Enabled`)

### Requirement: Nullable threshold override record

The system SHALL provide an `AlertThresholdOverrides` record in `Njord.Messages/Alerts/` that mirrors `AlertThresholdValues` with every field nullable, for use in partial config updates.

#### Scenario: AlertThresholdOverrides fields are all nullable
- **WHEN** an `AlertThresholdOverrides` instance is created with all nulls
- **THEN** it SHALL represent "no changes" and every property SHALL be null

#### Scenario: Partial override carries only changed fields
- **WHEN** an `AlertThresholdOverrides` instance has `FrostThresholds` set and all other fields null
- **THEN** only the frost threshold is considered changed

### Requirement: AlertSection wraps the shared record

`AlertSection` SHALL be a thin wrapper: `(bool Enabled, AlertThresholdValues Thresholds)`. The effective config mapping SHALL produce an `AlertSection` by wrapping the threshold values with the `Enabled` flag.

#### Scenario: AlertSection provides thresholds through the wrapper
- **WHEN** `EffectiveConfigMappings` maps `AlertOptions` to `AlertSection`
- **THEN** the result SHALL contain `Enabled` from the options and a nested `AlertThresholdValues` with all 16 threshold values

### Requirement: AlertConfigUpdate wraps the nullable record

`AlertConfigUpdate` SHALL be a thin wrapper: `(bool? Enabled, AlertThresholdOverrides? Thresholds)`. Null thresholds means "no threshold changes".

#### Scenario: Full config update with threshold changes
- **WHEN** a config update carries both `Enabled` and `Thresholds`
- **THEN** both SHALL be applied to the current `AlertOptions`

#### Scenario: Config update with only Enabled change
- **WHEN** a config update carries `Enabled = false` and `Thresholds = null`
- **THEN** only the enabled flag SHALL change; thresholds remain at their current values

### Requirement: Conversion bridges route through shared types

`AlertSettingsConversion.ToSettings()` SHALL accept `AlertThresholdValues` (converting `IReadOnlyList<double>` to `SeverityThresholds`) instead of reading fields directly from `AlertOptions`.

#### Scenario: AlertSettings produced from AlertThresholdValues
- **WHEN** `AlertThresholdValues` is converted to `AlertSettings`
- **THEN** multi-tier thresholds SHALL become `SeverityThresholds` and scalar thresholds SHALL pass through unchanged

### Requirement: Wire format compatibility

The gRPC proto messages and MQTT JSON payloads SHALL remain unchanged. The mapping layer SHALL absorb the structural change by navigating through the `Thresholds` wrapper.

#### Scenario: gRPC proto output unchanged
- **WHEN** an `AlertSection` with the new structure is mapped to its proto representation
- **THEN** the proto output SHALL be byte-identical to the current format

#### Scenario: MQTT payload unchanged
- **WHEN** alert results are published to MQTT
- **THEN** the JSON payload shape SHALL be identical to the current format
