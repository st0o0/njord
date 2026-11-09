## MODIFIED Requirements

### Requirement: v3 proto package with unified NjordService
The system SHALL define a `njord` proto package (file at `protos/njord/njord.proto`) containing a single `NjordService` with four RPCs: `GetManifest`, `Subscribe`, `GetStatus`, and `TriggerPoll`. The `csharp_namespace` SHALL be `Njord.Grpc`.

#### Scenario: Proto package is unversioned
- **WHEN** the proto file is compiled
- **THEN** the generated C# types SHALL live in the `Njord.Grpc` namespace, not `Njord.Grpc.V3`

#### Scenario: Proto file location is unversioned
- **WHEN** the proto file is referenced
- **THEN** it SHALL be at `protos/njord/njord.proto`, not under a `v3/` subdirectory

## REMOVED Requirements

### Requirement: v3 runs alongside v2
**Reason**: v2 is removed; v3 is now the only version and needs no coexistence
**Migration**: ha-njord must use the NjordService RPCs (GetManifest, Subscribe, GetStatus, TriggerPoll)

### Requirement: GetStatus and TriggerPoll preserve v2 semantics
**Reason**: v2 no longer exists; the "preserve v2" framing is obsolete. GetStatus and TriggerPoll retain their behavior but the requirement text referencing v2 equivalence is removed.
**Migration**: No migration needed; behavior is unchanged, only the spec text referencing v2 is removed.
