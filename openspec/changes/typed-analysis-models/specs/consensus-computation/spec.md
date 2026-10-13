## REMOVED Requirements

### Requirement: ConsensusResult serialization with pinned wire names
**Reason**: Consensus result records are in-process values only; nothing serializes or persists them, so the `[JsonProperty]` annotations on consensus records and on the referenced weather types are removed.
**Migration**: The published wire format is pinned by `wire-format-golden-masters`. The named records that replaced value tuples (`OutlierInfo`, `ConfidenceIntervalInfo`) stay.
