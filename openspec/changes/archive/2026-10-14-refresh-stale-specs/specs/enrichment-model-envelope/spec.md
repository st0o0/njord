## REMOVED Requirements

### Requirement: Per-model index scoring produces an envelope of min/max/confidence
**Reason**: Index envelopes are no longer computed by scoring each model separately; `IndexResult.Compute` does not exist. Each score's envelope is derived from the day slice's consensus confidence bounds (`DaySlice.Bounds`) inside `IIndexScore.Score`.
**Migration**: See `activity-indices` ("IndexResult includes per-day envelope for each activity score").

### Requirement: Envelope fields appear in discovery as additional sensor components
**Reason**: MQTT Discovery and the indices presenter were removed.
**Migration**: None; envelope fields are still published in the state payloads (requirement "Envelope fields appear in state payloads alongside existing values").
