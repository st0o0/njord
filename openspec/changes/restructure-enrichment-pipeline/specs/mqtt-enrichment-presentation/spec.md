## REMOVED Requirements

### Requirement: Presenters own MQTT presentation of enrichment results
**Reason**: The presenter pattern (`IEnrichmentPresenter` with `TypeName`
matching, 1:1 presenter-per-feature binding) is eliminated. MQTT becomes a
consumer that works directly with typed analysis results.
**Migration**: MQTT state publishing logic moves to the MQTT consumer which
receives typed results and formats state payloads directly — no presenter
indirection, no `TypeName` matching.

### Requirement: Presenters decide enablement from same config as features
**Reason**: With no presenters, enablement is handled per-feature in each
library's own options.
**Migration**: Each feature library owns its enabled/disabled toggle via its
own options type.

### Requirement: Presented payloads are byte-identical to pre-split output
**Reason**: Presenter pattern removed entirely.
**Migration**: State payload format is owned by the MQTT consumer. Verify
snapshots for state payloads are updated to reflect new output.

### Requirement: Discovery components built through shared spec/builder
**Reason**: Discovery is removed; no discovery payloads are built.
**Migration**: None — discovery capability is dropped.
