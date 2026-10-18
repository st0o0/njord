## Why

The same 16 alert threshold fields are duplicated across 4 types (`AlertOptions`, `AlertSettings`, `AlertSection`, `AlertConfigUpdate`), each with slightly different representations (`double[]`, `SeverityThresholds`, `IReadOnlyList<double>`, nullable `IReadOnlyList<double>?`). Adding a new alert type requires editing all 4 types plus their conversion bridges — a maintenance burden that grows with every new alert rule.

## What Changes

- Extract a shared `AlertThresholdValues` record in `Njord.Messages/Alerts/` that carries the 16 threshold fields as `IReadOnlyList<double>` (multi-tier) and `double`/`int` (scalar) — the config-layer representation.
- `AlertSection` becomes `(bool Enabled, AlertThresholdValues Thresholds)` — a thin wrapper.
- `AlertConfigUpdate` becomes `(bool? Enabled, AlertThresholdOverrides? Thresholds)` where `AlertThresholdOverrides` is the nullable mirror of `AlertThresholdValues`.
- `AlertSettings` keeps its `SeverityThresholds`-based shape (it is the domain representation consumed by alert rules) but its conversion from `AlertThresholdValues` is centralized.
- `AlertOptions` stays a mutable options class but gains a `ToThresholdValues()` conversion that the mapping layer uses.
- All mapping code (`EffectiveConfigMappings`, `ConfigUpdateMappings`, `ConfigResponseMappings`, `AlertSettingsConversion`) updated to route through the shared types.
- No wire-format changes: gRPC protos and MQTT payloads are unaffected (the mapping layer absorbs the structural change).

## Non-goals

- Changing `AlertSettings` to use `AlertThresholdValues` directly — the `SeverityThresholds` representation is consumed by alert rules and is the right domain type.
- Reducing `AlertOptions` properties — it's a mutable configuration class bound from `appsettings.json`; its shape is driven by the config format.
- Changing the proto or MQTT wire format.

## Capabilities

### New Capabilities

- `alert-threshold-values`: Shared alert threshold record that eliminates field duplication across the config/update/effective-config layer.

### Modified Capabilities

- `threshold-alerts`: The `AlertSettings` conversion path changes (routes through `AlertThresholdValues` instead of directly from options).

## Impact

- `Njord.Messages/Alerts/`: new `AlertThresholdValues` and `AlertThresholdOverrides` records
- `Njord.Messages/Config/EffectiveConfig.cs`: `AlertSection` simplified
- `Njord.Messages/Config/UpdateAnalysisConfig.cs`: `AlertConfigUpdate` simplified
- `Njord.Alerts/Configuration/AlertSettingsConversion.cs`: conversion updated
- `Njord/Configuration/EffectiveConfigMappings.cs`: mapping updated
- `Njord/Configuration/ConfigUpdateMappings.cs`: mapping updated
- `Njord.Grpc/Mapping/AlertProtoMappings.cs`: may need adjustment for new structure
- Tests: mapping specs, golden masters, and alert analyzer specs updated
