## ADDED Requirements

### Requirement: gRPC services delegate mapping to pure functions
Each gRPC service implementation in `Njord.Grpc` SHALL delegate request/response shaping
(building protobuf response messages from domain/actor-query results) to named, pure
mapper functions or static mapper classes, rather than inlining field-by-field construction
directly in an RPC method body. An RPC method SHALL read as: resolve input, query an
actor/registry if needed, call a mapper, return the result.

#### Scenario: Catalog mapping is independently testable
- **WHEN** testing how a model id and its `ModelCoverageRegistry` entry map to a `ModelInfo`
  proto message
- **THEN** the test calls `CatalogMapper.ToModelInfo(modelId)` directly, without a
  `ServerCallContext`, `ActorRegistry`, or running service instance

#### Scenario: Duplicated response shapes share one mapper
- **WHEN** two RPCs build structurally identical proto messages from the same source data
  (e.g. `OpsGrpcService.GetStatus`'s `ModelStatus` and `GetTargets`'s `TriggerTarget`, both
  derived from `PollStateEntry`)
- **THEN** both RPCs call the same shared mapper function for the shared fields, instead of
  each RPC independently re-implementing the field-by-field construction

#### Scenario: Wire output is unchanged by the mapping refactor
- **WHEN** an RPC is called with the same input before and after its mapping logic is moved
  into a named mapper function
- **THEN** the returned proto message is byte-for-byte identical
