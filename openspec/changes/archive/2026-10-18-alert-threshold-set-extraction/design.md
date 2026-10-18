## Context

Alert thresholds are carried by 4 types with 16 overlapping fields each:

| Type | Location | Representation | Role |
|------|----------|---------------|------|
| `AlertOptions` | Njord.Alerts/Configuration/ | `double[]` (mutable props) | Config binding from appsettings |
| `AlertSettings` | Njord.Messages/Alerts/ | `SeverityThresholds` (positional record) | Domain contract consumed by alert rules |
| `AlertSection` | Njord.Messages/Config/ | `IReadOnlyList<double>` (positional record) | Effective config response (gRPC + read model) |
| `AlertConfigUpdate` | Njord.Messages/Config/ | `IReadOnlyList<double>?` (nullable positional record) | Config update command |

Adding a new alert type requires editing all 4 types plus their conversion methods in 3 mapping classes.

## Goals / Non-Goals

**Goals:**
- Reduce the 4×16 field duplication to a shared record + thin wrappers
- Make adding a new alert type a single-record change (plus the rule itself)
- Keep `AlertSettings` as the domain representation with `SeverityThresholds`

**Non-Goals:**
- Changing `AlertOptions` to a record (it must stay a mutable class for config binding)
- Unifying `AlertSettings` and `AlertThresholdValues` (different type representations serve different consumers)
- Wire format changes (proto/MQTT)

## Decisions

### D1: Two new records in Njord.Messages/Alerts/

```
AlertThresholdValues(
    IReadOnlyList<double> FrostThresholds,
    IReadOnlyList<double> HeatThresholds,
    IReadOnlyList<double> StormGustThresholds,
    double HeavyRainHourlyThreshold,
    double HeavyRainDailyThreshold,
    double PressureDropThreshold,
    double PressureDropSevereThreshold,
    int FogPersistentHours,
    double CapeThreshold,
    double ThunderstormPrecipThreshold,
    double ThunderstormGustThreshold,
    double IceThreshold,
    IReadOnlyList<double> WindChillThresholds,
    IReadOnlyList<double> VisibilityThresholds,
    IReadOnlyList<double> TropicalNightThresholds,
    IReadOnlyList<double> HumidityThresholds)
```

`AlertThresholdOverrides` mirrors this with every field nullable.

**Why Njord.Messages?** Both `AlertSection` and `AlertConfigUpdate` already live there. The shared record is a contract type, not a domain computation type.

**Why not generic?** The threshold fields have different types (`IReadOnlyList<double>` vs `double` vs `int`), so a generic `ThresholdSet<T>` wouldn't eliminate the field declarations.

### D2: AlertSection and AlertConfigUpdate become 2-field wrappers

```csharp
sealed record AlertSection(bool Enabled, AlertThresholdValues Thresholds);
sealed record AlertConfigUpdate(bool? Enabled, AlertThresholdOverrides? Thresholds);
```

This is a **breaking change to the positional constructor** of both records. All call sites that construct them must update. Since these are internal contracts (no external consumers), this is safe.

### D3: AlertOptions gains a ToThresholdValues() extension

In `AlertSettingsConversion` (or a new `AlertThresholdConversion`), add:

```csharp
static AlertThresholdValues ToThresholdValues(this AlertOptions options)
```

This replaces the scattered field-by-field mapping in `EffectiveConfigMappings.ToSection()` and becomes the single source for Options → config-layer conversion.

### D4: AlertSettingsConversion.ToSettings() takes AlertThresholdValues

```csharp
static AlertSettings ToSettings(this AlertThresholdValues values)
```

Instead of `ToSettings(AlertOptions options)`. The conversion `IReadOnlyList<double>` → `SeverityThresholds` is centralized here.

The call chain becomes: `AlertOptions` → `.ToThresholdValues()` → `.ToSettings()` for the pipeline, and `AlertOptions` → `.ToThresholdValues()` → wrapped in `AlertSection` for the config read model.

### D5: Field order in AlertThresholdValues matches AlertSection (minus Enabled)

The current `AlertConfigUpdate` has a slightly different field order from `AlertSection` (PressureDropSevereThreshold and FogPersistentHours are swapped). The shared record normalizes to `AlertSection`'s order. This is an internal-only change.

## Risks / Trade-offs

- **[Risk]** Proto mapping reads `AlertSection` fields positionally → **Mitigation**: Proto mappings use named property access, not deconstruction. The `Thresholds.` prefix is the only change.
- **[Risk]** Golden master snapshots break → **Mitigation**: Update the `ProtoMappingGoldenMasterSpec` and `DefaultOptionsSpec` verified files. The wire output is unchanged; only the C# object structure differs.
- **[Risk]** Config update application logic needs adjustment → **Mitigation**: `ConfigUpdateMappings` already maps field-by-field from `AlertConfigUpdate` to `AlertOptions`. It now navigates through `Thresholds.` for the threshold fields.

## Open Questions

None — the approach is straightforward record extraction with no ambiguity.
