# grpc-mapping-layer Specification

## MODIFIED Requirements

### Requirement: gRPC services delegate mapping to pure functions
Each gRPC service implementation in `Njord.Grpc` SHALL delegate response shaping to pure `ToProto()` extension methods in `Njord.Grpc/Mapping/`, grouped per OutputKind (weather, alert, score, condition, trend, forecast, observation). The `Mapping/` directory SHALL be flat (no version subfolders). Mapper classes SHALL live in the `Njord.Grpc.Mapping` namespace. An RPC method SHALL read as: map the request, ask an actor resolved by marker, map the result, return it. Mappers SHALL take only message results and request protos as input; they SHALL NOT read options or domain registries.

The `ModelStatus` proto message SHALL include a `repeated DomainPollStatus domain_states` field exposing the per-domain learning state. Each `DomainPollStatus` entry SHALL contain the domain name, phase (Discovery/Steady/Suspended), learned cycle seconds (if any), miss count, and next poll timestamp. The `ToModelStatus` mapping extension SHALL populate these fields from the `PollStateEntry` domain states.

#### Scenario: Mapping is per-OutputKind, not per-feature
- **WHEN** a new alert feature is added (e.g. HailAlert)
- **THEN** no new mapping extension class SHALL be needed; the existing alert `ToProto()` mapper SHALL handle it

#### Scenario: No versioned mapping subfolders exist
- **WHEN** the Mapping directory is listed
- **THEN** there SHALL be no `V2/` or `V3/` subdirectories; all mappers SHALL be at the Mapping root

#### Scenario: Wire output preserves feature identity
- **WHEN** a feature result is mapped to an `EntityStateUpdate` proto
- **THEN** the `entity_id` and `feature` fields SHALL identify which feature produced it, even though the value type is generic per OutputKind

#### Scenario: Duplicated response shapes share one mapper
- **WHEN** two RPCs build structurally identical proto messages from the same source data
- **THEN** both RPCs call the same shared mapper function for the shared fields

#### Scenario: Per-domain poll state is included in GetStatus ModelStatus
- **WHEN** GetStatus is called and a location has domains in different learning phases
- **THEN** the ModelStatus response SHALL include a DomainPollStatus entry per active domain showing its individual phase, learned cycle, miss count, and next poll time
