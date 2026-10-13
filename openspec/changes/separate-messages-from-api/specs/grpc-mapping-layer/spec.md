## MODIFIED Requirements

### Requirement: gRPC services delegate mapping to pure functions
Each gRPC service implementation in `Njord.Grpc` SHALL delegate response shaping to pure `ToProto()` extension methods over message results, grouped per domain (forecast, analysis, config, catalog, ops, sensors), rather than inlining field-by-field construction in an RPC method body. Request shaping SHALL likewise use pure `ToMessage()`/`ToCommand()` mappings. An RPC method SHALL read as: map the request, ask an actor resolved by marker, map the result, return it. Mappers SHALL take only message results and request protos as input; they SHALL NOT read options or domain registries.

#### Scenario: Catalog mapping is independently testable
- **WHEN** testing how a catalog result entry (model id with its coverage) maps to a `ModelInfo` proto message
- **THEN** the test calls the catalog `ToProto()` mapping directly, without a `ServerCallContext`, `ActorRegistry`, running service instance or coverage registry

#### Scenario: Duplicated response shapes share one mapper
- **WHEN** two RPCs build structurally identical proto messages from the same source data
  (e.g. the ops status `ModelStatus` and the trigger-target `TriggerTarget`, both
  derived from a poll state entry)
- **THEN** both RPCs call the same shared mapper function for the shared fields, instead of
  each RPC independently re-implementing the field-by-field construction

#### Scenario: Wire output is unchanged by the mapping refactor
- **WHEN** an RPC is called with the same input before and after its mapping logic is moved
  into the per-domain mappings
- **THEN** the returned proto message is byte-for-byte identical
