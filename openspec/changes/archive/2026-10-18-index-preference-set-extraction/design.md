## Context

`IndexConfigUpdate` carries 12 positional params: `bool? Enabled` + 11 `double?` preference fields. `IndexSection` already wraps `IndexPreferenceValues` for the read path. The write path should follow the same pattern.

`IndexPreferenceValues` is already all-nullable (`double?`), so it serves as its own override type — unlike the alert case which needed a separate `AlertThresholdOverrides`.

## Goals / Non-Goals

**Goals:**
- `IndexConfigUpdate` becomes `(bool? Enabled, IndexPreferenceValues? Preferences)`
- Symmetry between read path (`IndexSection` wraps `IndexPreferenceValues`) and write path (`IndexConfigUpdate` wraps `IndexPreferenceValues?`)

**Non-Goals:**
- Eliminating `IndexPreferences` ↔ `IndexPreferenceValues` copy (different layers)
- Changing `ResolvedPreferences` (different domain names)

## Decisions

### D1: Reuse IndexPreferenceValues as the override type

Since `IndexPreferenceValues` is already all `double?`, null fields naturally mean "no change". No separate `IndexPreferenceOverrides` needed. `Preferences = null` means "no preference changes at all".

### D2: ConfigUpdateMappings navigates through .Preferences.

The `ApplyTo` method currently reads `update.IndoorTemp`, `update.IdealOutdoorTemp`, etc. After: `update.Preferences?.IndoorTemp`, `update.Preferences?.IdealOutdoorTemp`. The null-conditional `?.` handles both "no preferences object" and "field is null within preferences".

### D3: Field order normalizes to IndexPreferenceValues order

`IndexConfigUpdate` currently has a different field order than `IndexPreferenceValues`. The wrapper eliminates this divergence.

## Risks / Trade-offs

- **[Risk]** ConfigRequestMappings constructs IndexConfigUpdate from proto fields → **Mitigation**: Build an `IndexPreferenceValues` from the proto fields, wrap it.
- **[Risk]** Null semantics change subtly → **Mitigation**: `Preferences = null` (no changes) vs `Preferences = new(null, null, ...)` (explicit all-null) are distinguishable but both mean "no changes" in practice. The `ApplyTo` logic already checks each field individually.
