## REMOVED Requirements

### Requirement: GetConfig returns current njord configuration
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Covered by `grpc-v2-admin-service` (`GetConfig`, `StreamConfig`, the six-message enrichment config, `BudgetProjection`); status and trigger targets moved to `grpc-v2-ops-service`.

### Requirement: StreamConfig pushes config changes
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Covered by `grpc-v2-admin-service` (`GetConfig`, `StreamConfig`, the six-message enrichment config, `BudgetProjection`); status and trigger targets moved to `grpc-v2-ops-service`.

### Requirement: ConfigService is a separate gRPC service
**Reason**: The v1 `njord.v1` protos and services (`ConfigService`, `ForecastService`) no longer exist; only `protos/njord/v2` is built.
**Migration**: Covered by `grpc-v2-admin-service` (`GetConfig`, `StreamConfig`, the six-message enrichment config, `BudgetProjection`); status and trigger targets moved to `grpc-v2-ops-service`.
