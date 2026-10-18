## Why

`IndexConfigUpdate` flattens 11 index preference fields alongside `Enabled` (12 positional params, all nullable). `IndexSection` already wraps `IndexPreferenceValues` — so the config read side is clean, but the config write side repeats the fields. Adding a new index preference requires editing `IndexConfigUpdate` separately from `IndexPreferenceValues`, and the field order already diverges between the two.

## What Changes

- `IndexConfigUpdate` simplified from `(bool? Enabled, double? IndoorTemp, double? IdealOutdoorTemp, ...)` to `(bool? Enabled, IndexPreferenceValues? Preferences)`.
- No new records needed — `IndexPreferenceValues` is already all-nullable `double?` and serves as its own override type.
- `ConfigUpdateMappings.ApplyTo()` navigates through `.Preferences.` for preference fields.
- `ConfigRequestMappings` (gRPC) constructs the new shape.
- Wire format unchanged — mapping layer absorbs the structural change.

## Non-goals

- Eliminating the `IndexPreferences` → `IndexPreferenceValues` 1:1 copy (different layers: mutable options class vs immutable contract record).
- Changing `ResolvedPreferences` (uses different domain-oriented names, rightfully a separate type).
- Unifying per-score and per-location override structures.

## Capabilities

### New Capabilities

_None — this reuses the existing `IndexPreferenceValues` record._

### Modified Capabilities

- `index-preferences`: `IndexConfigUpdate` wraps preferences instead of flattening them.

## Impact

- `Njord.Messages/Config/UpdateAnalysisConfig.cs`: `IndexConfigUpdate` simplified
- `Njord/Configuration/ConfigUpdateMappings.cs`: preference application navigates through wrapper
- `Njord.Grpc/Mapping/ConfigRequestMappings.cs`: constructs new shape
- Tests: ConfigUpdateMappingsSpec, ConfigManagerSpec, ConfigRequestMappingsSpec updated
