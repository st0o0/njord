## MODIFIED Requirements

### Requirement: gRPC services delegate mapping to pure functions
Each gRPC service implementation in `Njord.Grpc` SHALL delegate response shaping to pure `ToProto()` extension methods in `Njord.Grpc/Mapping/`, grouped per OutputKind (weather, alert, score, condition, trend, forecast, observation). The `Mapping/` directory SHALL be flat (no version subfolders). Mapper classes SHALL live in the `Njord.Grpc.Mapping` namespace.

#### Scenario: Mapping is per-OutputKind, not per-feature
- **WHEN** a new alert feature is added (e.g. HailAlert)
- **THEN** no new mapping extension class SHALL be needed; the existing alert `ToProto()` mapper SHALL handle it

#### Scenario: No versioned mapping subfolders exist
- **WHEN** the Mapping directory is listed
- **THEN** there SHALL be no `V2/` or `V3/` subdirectories; all mappers SHALL be at the Mapping root

## REMOVED Requirements

### Requirement: v2 mapping extensions
**Reason**: v2 services and proto are removed. All v2-specific mapping classes (AlertResultMappingExtensions, EnrichmentEventMappingExtensions, FeatureResultMappingExtensions, FeatureEnumMappingExtensions, OpsProtoMappingExtensions, DomainDataProtoMappingExtensions) are deleted.
**Migration**: All consumers use the NjordService (entity-first) API with per-OutputKind mappers.
