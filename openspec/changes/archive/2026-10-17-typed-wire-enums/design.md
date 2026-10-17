## Context

The C# domain already has `TrendDirection`, `Stability`, `DewPointComfort`, `PollPhase`, `ConsensusMethod`, `AlertSeverity` with `ToWireName()` extensions. The weak points are the places that bypass them: name-based conversion (`ConsensusMethod`, MQTT severity, metric tag), a ternary (`PollPhase`), and a free `string` (`VpdInfo.Category`). On the proto side only `CoverageTier`, `AlertType`, `AlertSeverity` and `SensorKind` are enums so far.

## Goals / Non-Goals

**Goals:** one explicit, exhaustive mapping per closed set; gRPC carries enums for the closed sets; no behaviour change for MQTT and metrics.

**Non-Goals:** open sets (`parameter`, `unit`, `horizon`, `source`), persistence, sensor rejection enum.

## Decisions

1. **Proto: new field number, old one reserved.** Changing `string` to an enum on the same number is not wire compatible (length-delimited vs varint), so each field gets a new number and the old number is `reserved` (the field name is reused by the new enum field), as when `history` was removed. Alternative (keep both fields for a release) rejected: 0.5.0 is a coordinated breaking release.
2. **Every enum has `<NAME>_UNSPECIFIED = 0`.** Optional string fields become `optional <Enum>`; an absent value is "not delivered", never silently mapped to the first member.
3. **Domain enums stay the source of truth; mappings are exhaustive switches with a throwing default** (the existing `ToWireName()` style). `PollPhase` gets the same treatment; no new `ToWireName` is needed for the proto since the mapping targets proto enum members, but `ToWireName()` is kept for MQTT/metrics where text is still emitted.
4. **`VpdCategory` in `Njord.Compute/Contracts`** next to its sibling enums, with `ToWireName()` returning `low/optimal/high/critical` so MQTT stays identical.
5. **`ConsensusMethod` over gRPC has `MEAN`, `MEDIAN`, `TRIMMED_MEAN`.** On `SetEnrichment`, `UNSPECIFIED`/absent means "leave unchanged"; an unknown numeric value from a newer client is answered with a Rejected `ConfigResponse` instead of being treated as unset. `NjordConfig` always reports the real method.
6. **Metric tag**: an explicit `OutcomeTag(...)` switch yields the same labels as today (`success`, `unknown`, lowercased failure reasons) so dashboards do not change; a new reason fails a test instead of silently producing a new label.
7. **`EnrichmentEvent.type_name` stays a string.** It is redundant with the `payload` oneof, which ha-njord already switches on, so an enum would duplicate information. Removing the field is a candidate follow-up, not part of this change.
8. **Client tolerance**: the contract documents that clients must treat unknown enum numbers as `UNSPECIFIED`; protobuf already delivers unknown values as the raw number.

## Risks / Trade-offs

- ha-njord built against old protos breaks until it syncs → mitigated by releasing both as 0.5.0 and by the ha-njord change.
- Golden masters and Verify snapshots change for the gRPC output → reviewed as part of the tasks; MQTT snapshots must stay unchanged and act as the regression guard.
- New field numbers in `common.proto` leave gaps → intended and noted with `reserved`.
