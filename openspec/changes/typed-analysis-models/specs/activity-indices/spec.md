## REMOVED Requirements

### Requirement: DaySliceIndexResult serialization with pinned wire names
**Reason**: Index result records are in-process values only; nothing serializes or persists them, so the `[JsonProperty]` annotations are removed.
**Migration**: The MQTT wire keys (including `night_ventilation`) are pinned by `wire-format-golden-masters`.
