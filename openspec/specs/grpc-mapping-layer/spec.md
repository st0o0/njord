# grpc-mapping-layer Specification

## Purpose

Keeps the gRPC services thin: every mapping from domain results and messages to proto messages lives in pure `ToProto()`/`ToCommand()` extension functions in `Njord.Grpc/Mapping/`.

## Requirements

### Requirement: gRPC services delegate mapping to pure functions
Each gRPC service implementation in `Njord.Grpc` SHALL delegate response shaping to pure `ToProto()` extension methods in `Njord.Grpc/Mapping/`, grouped per OutputKind (weather, alert, score, condition, trend, forecast, observation). The `Mapping/` directory SHALL be flat (no version subfolders). Mapper classes SHALL live in the `Njord.Grpc.Mapping` namespace. An RPC method SHALL read as: map the request, ask an actor resolved by marker, map the result, return it. Mappers SHALL take only message results and request protos as input; they SHALL NOT read options or domain registries.

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
  (e.g. the ops status `ModelStatus` and the trigger-target `TriggerTarget`, both
  derived from a poll state entry)
- **THEN** both RPCs call the same shared mapper function for the shared fields, instead of
  each RPC independently re-implementing the field-by-field construction

### Requirement: Generic OutputKind mappers replace per-feature switch
The mapping layer SHALL provide one mapper per OutputKind that converts any feature result of that kind to the corresponding proto value type. There SHALL be no switch or conditional on individual feature types. The mapper SHALL extract the common fields defined by the OutputKind base result (e.g. severity + trigger for alerts, value + label for scores).

#### Scenario: Alert mapper handles any alert feature
- **WHEN** `FrostAlertResult` and `HeatAlertResult` are both mapped
- **THEN** both SHALL be handled by the same alert mapper producing `AlertValue` with severity and trigger fields

#### Scenario: Adding a feature requires zero mapping changes
- **WHEN** a new feature is registered with `OutputKind.Score`
- **THEN** the existing score mapper SHALL handle its result without modification, and the manifest SHALL include it without modification

### Requirement: Entity state mapping from PipelineEvent
The mapping layer SHALL convert `PipelineEvent` messages (containing `FeatureResult` instances) to `EntityStateUpdate` proto messages. The conversion SHALL resolve the `entity_id` from the feature type and location, select the value mapper by OutputKind, and set the `updated_at` timestamp.

#### Scenario: PipelineEvent maps to EntityStateUpdate
- **WHEN** a PipelineEvent carrying a FrostAlertResult for Vienna is mapped
- **THEN** the result SHALL be an `EntityStateUpdate` with `entity_id = "vienna_frost"`, `updated_at` set, and an `AlertValue` in the value oneof
